// Controllers/SampleStyleController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using System.Security.Claims;

namespace CpPrinting.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SampleStyleController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];
        private const long MaxImageBytes = 10 * 1024 * 1024;

        public SampleStyleController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private string CurrentUser => User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        private string Now => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        // ── Helper: derive OriginalImagePath at response time ─────────────────
        // OriginalImagePath is [NotMapped] — computed from revision history, no DB column.
        private static void PopulateOriginalImagePath(SampleStyle style)
        {
            if (style.Revisions != null && style.Revisions.Count > 0)
            {
                var first = style.Revisions.OrderBy(r => r.RevisionNo).First();
                style.OriginalImagePath = first.PreviousArtworkUrl ?? style.ImagePath;
            }
            else
            {
                style.OriginalImagePath = style.ImagePath;
            }
        }

        // ==========================================
        // GET ALL / GET BY ID
        // ==========================================

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SampleStyle>>> GetAll(
            [FromQuery] string? customer         = null,
            [FromQuery] string? styleNo          = null,
            [FromQuery] string? adminStatus      = null,
            [FromQuery] bool?   clientApproved   = null,
            [FromQuery] bool?   submittedToAdmin = null)
        {
            var query = _context.SampleStyles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(customer))
                query = query.Where(s => s.Customer.ToLower().Contains(customer.ToLower()));
            if (!string.IsNullOrWhiteSpace(styleNo))
                query = query.Where(s => s.StyleNo.ToLower().Contains(styleNo.ToLower()));
            if (!string.IsNullOrWhiteSpace(adminStatus))
                query = query.Where(s => s.AdminStatus == adminStatus);
            if (clientApproved.HasValue)
                query = query.Where(s => s.ClientApproved == clientApproved.Value);
            if (submittedToAdmin.HasValue)
                query = query.Where(s => s.SubmittedToAdmin == submittedToAdmin.Value);

            var styles = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            foreach (var s in styles) PopulateOriginalImagePath(s);
            return Ok(styles);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SampleStyle>> GetById(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // CREATE
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost]
        public async Task<ActionResult<SampleStyle>> Create([FromBody] SampleStyle style)
        {
            style.Id               = Guid.NewGuid().ToString();
            style.AdminStatus      = "Pending";
            style.ClientApproved   = false;
            style.SubmittedToAdmin = false;
            style.Revisions        = new List<SampleStyleRevision>();
            style.CreatedAt        = Now;
            style.UpdatedAt        = Now;
            _context.SampleStyles.Add(style);
            await _context.SaveChangesAsync();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // IMAGE UPLOAD
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("{id}/image")]
        [RequestSizeLimit(10_485_760)]
        public async Task<ActionResult<SampleStyle>> UploadImage(string id, IFormFile file)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (file == null || file.Length == 0)     return BadRequest("No file uploaded.");
            if (file.Length > MaxImageBytes)           return BadRequest("File exceeds 10 MB limit.");
            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed.");

            var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "samples");
            Directory.CreateDirectory(uploadsDir);

            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var old = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            }

            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream);

            style.ImagePath = $"/uploads/samples/{fileName}";
            style.UpdatedAt = Now;
            await _context.SaveChangesAsync();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // ADD REVISION
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("{id}/revisions")]
        public async Task<ActionResult<SampleStyle>> AddRevision(
            string id, [FromBody] AddRevisionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Comment))
                return BadRequest("Comment is required.");

            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (style.ClientApproved)
                return BadRequest("Style is already client-approved. No more revisions can be added.");

            style.Revisions ??= new List<SampleStyleRevision>();

            var previousArtwork = style.ImagePath;
            string? newArtworkUrl = null;
            if (!string.IsNullOrWhiteSpace(dto.ArtworkUrl))
            {
                newArtworkUrl   = dto.ArtworkUrl.Trim();
                style.ImagePath = newArtworkUrl;
            }

            var newEntry = new SampleStyleRevision
            {
                Id                 = Guid.NewGuid().ToString(),
                RevisionNo         = style.Revisions.Count + 1,
                Comment            = dto.Comment.Trim(),
                PreviousArtworkUrl = previousArtwork,
                ArtworkUrl         = newArtworkUrl,
                CreatedAt          = Now,
                CreatedBy          = CurrentUser,
            };

            style.Revisions = new List<SampleStyleRevision>(style.Revisions) { newEntry };
            style.UpdatedAt = Now;
            _context.Entry(style).Property(e => e.Revisions).IsModified = true;
            await _context.SaveChangesAsync();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // MARK CLIENT APPROVED
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/clientapprove")]
        public async Task<ActionResult<SampleStyle>> MarkClientApproved(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
            if (style.SubmittedToAdmin)
                return BadRequest("Cannot change client approval after submission to admin.");

            style.ClientApproved   = !style.ClientApproved;
            style.ClientApprovedAt = style.ClientApproved ? Now : null;
            style.ClientApprovedBy = style.ClientApproved ? CurrentUser : null;
            style.UpdatedAt        = Now;
            await _context.SaveChangesAsync();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // SUBMIT TO ADMIN
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/submit")]
        public async Task<ActionResult<SampleStyle>> SubmitToAdmin(
            string id, [FromBody] SubmitToAdminDto dto)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
            if (!style.ClientApproved)   return BadRequest("Style must be client-approved before submitting to admin.");
            if (style.SubmittedToAdmin)  return BadRequest("Already submitted to admin.");
            if (string.IsNullOrWhiteSpace(dto.RcMeetingDate)) return BadRequest("RC Meeting Date is required.");
            if (string.IsNullOrWhiteSpace(dto.BulkQty))       return BadRequest("Bulk Qty is required.");

            style.RcMeetingDate     = dto.RcMeetingDate.Trim();
            style.BoardSet          = dto.BoardSet?.Trim();
            style.BulkQty           = dto.BulkQty.Trim();
            style.DeveloperComments = dto.DeveloperComments?.Trim();
            style.SubmittedToAdmin  = true;
            style.SubmittedAt       = Now;
            style.AdminStatus       = "Pending";
            style.UpdatedAt         = Now;

            var existing = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == style.Id);
            if (existing == null)
            {
                var latestRev = style.Revisions?.OrderByDescending(r => r.RevisionNo).FirstOrDefault();
                var comment = latestRev != null
                    ? $"Rev {latestRev.RevisionNo}: {latestRev.Comment}"
                    : style.DeveloperComments ?? $"Sample style submitted — {style.StyleNo} ({style.Component})";
                _context.Submissions.Add(new SubmissionForm
                {
                    Id = style.Id, StyleNo = style.StyleNo, CustomerName = style.Customer,
                    SubmissionDate = Now, Level = "Sample", Comment = comment,
                    RevisionNo = latestRev?.RevisionNo ?? 1, IsLatestRevision = true,
                });
            }

            await _context.SaveChangesAsync();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // ADMIN ACTION
        // ==========================================

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/adminaction")]
        public async Task<ActionResult<SampleStyle>> AdminAction(
            string id, [FromBody] AdminActionDto dto)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
            if (!style.SubmittedToAdmin) return BadRequest("Style has not been submitted to admin yet.");

            var hasStoreIn = await _context.StoreInRecords.AnyAsync(s => s.SubmissionId == style.Id);
            if (hasStoreIn)
                return BadRequest("LOCKED: Store-In records already exist for this style. " +
                                  "Approval cannot be changed once goods have been received.");

            var allowed = new[] { "Approved", "Pending" };
            if (!allowed.Contains(dto.Status)) return BadRequest("Status must be 'Approved' or 'Pending'.");

            style.AdminStatus   = dto.Status;
            style.AdminRemarks  = dto.Remarks?.Trim();
            style.AdminActionAt = Now;
            style.AdminActionBy = CurrentUser;
            style.UpdatedAt     = Now;

            var latestRevision = style.Revisions?.OrderByDescending(r => r.RevisionNo).FirstOrDefault();
            var submissionComment = latestRevision != null
                ? $"Rev {latestRevision.RevisionNo}: {latestRevision.Comment}"
                : style.DeveloperComments ?? "Sample style approval";

            var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == style.Id);
            if (submission == null)
            {
                submission = new SubmissionForm
                {
                    Id = style.Id, StyleNo = style.StyleNo, CustomerName = style.Customer,
                    SubmissionDate = style.SubmittedAt ?? Now, Level = "Sample",
                    Comment = submissionComment, RevisionNo = latestRevision?.RevisionNo ?? 1,
                    IsLatestRevision = true,
                };
                _context.Submissions.Add(submission);
            }

            var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == style.Id);
            if (dto.Status == "Approved")
            {
                if (approval == null)
                {
                    _context.Approvals.Add(new ApprovalRecord
                    {
                        Id = Guid.NewGuid().ToString(), SubmissionId = style.Id,
                        StyleNo = style.StyleNo, CustomerName = style.Customer,
                        Level = "Sample", RevisionNo = latestRevision?.RevisionNo ?? 1,
                        Status = "Approved", BoardSet = style.BoardSet,
                        ApprovalCard = style.AcNumber, RaMeetingDate = style.RcMeetingDate,
                        BulkOrderQty = style.BulkQty, ReviewedAt = Now,
                    });
                }
                else
                {
                    approval.Status = "Approved"; approval.BoardSet = style.BoardSet;
                    approval.ApprovalCard = style.AcNumber; approval.RaMeetingDate = style.RcMeetingDate;
                    approval.BulkOrderQty = style.BulkQty; approval.ReviewedAt = Now;
                    approval.StyleNo = style.StyleNo; approval.CustomerName = style.Customer;
                }
            }
            else if (approval != null)
            {
                approval.Status = "Pending"; approval.BoardSet = null;
                approval.ApprovalCard = null; approval.RaMeetingDate = null;
                approval.BulkOrderQty = null; approval.ReviewedAt = Now;
            }

            await _context.SaveChangesAsync();
            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ==========================================
        // ADD EXTRA BULK QTY / CHANGE BODY COLOUR
        // PATCH /api/samplestyle/{id}/revise
        //
        // FIX: This now UPDATES the existing ApprovalRecord instead of
        // creating new SampleStyle + Submission + Approval records.
        //
        // The extra qty is added directly to the existing BulkOrderQty.
        // Result: ONE approval, ONE bulk qty, ONE card in Store-In.
        //
        // Example: existing BulkOrderQty=500, extraBulkQty=100 → new BulkOrderQty=600
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/revise")]
        public async Task<ActionResult<SampleStyle>> AddExtraBulk(
            string id, [FromBody] ReviseStyleDto dto)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (style.AdminStatus != "Approved")
                return BadRequest("Only approved styles can have extra bulk added.");

            if (string.IsNullOrWhiteSpace(dto.ExtraBulkQty) ||
                !int.TryParse(dto.ExtraBulkQty, out var extraQty) || extraQty <= 0)
                return BadRequest("Extra Bulk Qty must be a positive number.");

            // Find the existing approval for this style
            var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == style.Id);
            if (approval == null)
                return BadRequest("No approval record found for this style. Contact admin.");

            if (approval.Status != "Approved")
                return BadRequest("Approval is not in Approved status.");

            // Calculate new total
            var currentBulk = int.TryParse(approval.BulkOrderQty, out var cb) ? cb : 0;
            var newTotal = currentBulk + extraQty;

            // Validate: cannot set total below already received
            var totalAlreadyReceived = await _context.StoreInRecords
                .Where(s => s.SubmissionId == style.Id)
                .SumAsync(s => s.InQty);

            if (newTotal < totalAlreadyReceived)
                return BadRequest(
                    $"Cannot set total bulk to {newTotal}: already received {totalAlreadyReceived} units. " +
                    $"Extra qty would result in a total less than already received.");

            var oldBulk     = approval.BulkOrderQty;
            var oldColour   = style.BodyColour;
            var colourChanged = !string.IsNullOrWhiteSpace(dto.NewBodyColour) &&
                                dto.NewBodyColour.Trim() != style.BodyColour;

            // Update the existing ApprovalRecord — no new records created
            approval.BulkOrderQty = newTotal.ToString();
            approval.ReviewedAt   = Now;

            // Update SampleStyle display field and optionally body colour
            style.BulkQty   = newTotal.ToString();
            style.UpdatedAt = Now;

            if (colourChanged)
                style.BodyColour = dto.NewBodyColour!.Trim();

            // Record the change as a note in DeveloperComments
            var note = $"[{Now}] Extra bulk +{extraQty} added by {CurrentUser}. " +
                       $"Total: {oldBulk} → {newTotal}.";
            if (colourChanged)
                note += $" Colour: {oldColour} → {dto.NewBodyColour!.Trim()}.";
            if (!string.IsNullOrWhiteSpace(dto.Comments))
                note += $" Note: {dto.Comments.Trim()}";

            style.DeveloperComments = string.IsNullOrWhiteSpace(style.DeveloperComments)
                ? note
                : style.DeveloperComments + "\n" + note;

            // Also update the linked Submission comment for audit trail.
            // IMPORTANT: Restore IsLatestRevision = true on the original submission.
            // The old POST /revise (now replaced) incorrectly set this to false, blocking
            // Store-In with "Only the latest approved revision can move to Stores."
            // This self-heals that data corruption every time extra bulk is added.
            var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == style.Id);
            if (submission != null)
            {
                submission.Comment          = submission.Comment + $" | +{extraQty} extra bulk added {Now}";
                submission.IsLatestRevision = true;  // restore — this IS the canonical submission
            }

            await _context.SaveChangesAsync();

            // Sync denormalized StoreInRecord.BulkQty display fields
            try
            {
                await SyncStoreInBulkQty(style.Id, newTotal, totalAlreadyReceived);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SampleStyleController] SyncStoreInBulkQty failed (non-fatal): {ex.Message}");
            }

            PopulateOriginalImagePath(style);
            return Ok(style);
        }

        // ── Sync denormalized display fields on StoreInRecords ────────────────
        // Called after bulk qty changes. Non-fatal if it fails.
        private async Task SyncStoreInBulkQty(string submissionId, int newBulkQty, int totalAlreadyReceived)
        {
            var storeInRecords = await _context.StoreInRecords
                .Where(s => s.SubmissionId == submissionId)
                .ToListAsync();

            foreach (var record in storeInRecords)
            {
                record.BulkQty        = newBulkQty;
                record.BalanceBulkQty = Math.Max(0, newBulkQty - totalAlreadyReceived);
            }

            if (storeInRecords.Any())
                await _context.SaveChangesAsync();
        }

        // ==========================================
        // REVISION ARTWORK UPLOAD
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("revisionimage")]
        [RequestSizeLimit(10_485_760)]
        public async Task<ActionResult> UploadRevisionImage(IFormFile file)
        {
            if (file == null || file.Length == 0)     return BadRequest("No file uploaded.");
            if (file.Length > MaxImageBytes)           return BadRequest("File exceeds 10 MB limit.");
            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed.");

            var dir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "revisions");
            Directory.CreateDirectory(dir);

            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"rev_{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(dir, fileName);
            using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return Ok(new { url = $"{baseUrl}/uploads/revisions/{fileName}" });
        }

        // ==========================================
        // SERVE SAMPLE IMAGE
        // ==========================================

        [HttpGet("image")]
        public IActionResult GetImage([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/uploads/"))
                return BadRequest("Invalid path.");
            var filePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, path.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound();
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var ct  = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"  => "image/png",
                ".webp" => "image/webp",
                _       => "application/octet-stream",
            };
            return PhysicalFile(filePath, ct);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var fp = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(fp)) System.IO.File.Delete(fp);
            }
            _context.SampleStyles.Remove(style);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    // ==========================================
    // DTOs — original names preserved
    // ==========================================

    public class AddRevisionDto
    {
        public string  Comment    { get; set; } = string.Empty;
        public string? ArtworkUrl { get; set; }
    }

    public class SubmitToAdminDto
    {
        public string  RcMeetingDate     { get; set; } = string.Empty;
        public string? BoardSet          { get; set; }
        public string  BulkQty           { get; set; } = string.Empty;
        public string? DeveloperComments { get; set; }
    }

    public class ReviseStyleDto
    {
        /// <summary>Extra qty to add on top of the existing approved bulk.</summary>
        public string  ExtraBulkQty  { get; set; } = string.Empty;
        public string? RcMeetingDate { get; set; }
        public string? AcNumber      { get; set; }
        public string? BoardSet      { get; set; }
        public string? Comments      { get; set; }
        /// <summary>
        /// Optional. If provided, replaces the existing body colour.
        /// Leave null/empty to keep the existing colour unchanged.
        /// </summary>
        public string? NewBodyColour { get; set; }
    }

    public class AdminActionDto
    {
        public string  Status  { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}