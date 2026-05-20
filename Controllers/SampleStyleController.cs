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

            return Ok(await query.OrderByDescending(s => s.CreatedAt).ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SampleStyle>> GetById(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
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
            return Ok(style);
        }

        // ==========================================
        // IMAGE UPLOAD
        // POST /api/samplestyle/{id}/image
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("{id}/image")]
        [RequestSizeLimit(10_485_760)]
        public async Task<ActionResult<SampleStyle>> UploadImage(string id, IFormFile file)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");
            if (file.Length > MaxImageBytes)
                return BadRequest("File exceeds 10 MB limit.");
            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed.");

            var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "samples");
            Directory.CreateDirectory(uploadsDir);

            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var oldFile = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
            }

            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream);

            style.ImagePath = $"/uploads/samples/{fileName}";
            style.UpdatedAt = Now;
            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ==========================================
        // ADD REVISION
        // Each time the client gives feedback, the
        // developer adds a comment here.
        // System auto-numbers: Rev 1, Rev 2, Rev 3...
        // Blocked once ClientApproved = true.
        //
        // POST /api/samplestyle/{id}/revisions
        // Body: { "comment": "Client asked to fix the colour on front panel" }
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

            var newEntry = new SampleStyleRevision
            {
                Id         = Guid.NewGuid().ToString(),
                RevisionNo = style.Revisions.Count + 1,
                Comment    = dto.Comment.Trim(),
                CreatedAt  = Now,
                CreatedBy  = CurrentUser,
            };

            // Replace the list entirely so EF detects the JSON column as modified.
            // Mutating in-place (.Add) does NOT trigger EF change tracking on JSON columns.
            style.Revisions = new List<SampleStyleRevision>(style.Revisions) { newEntry };
            style.UpdatedAt = Now;

            // Explicitly mark Revisions as modified (required for JSON columns)
            _context.Entry(style).Property(e => e.Revisions).IsModified = true;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ==========================================
        // MARK CLIENT APPROVED
        // Developer marks this style as client-approved
        // after all revisions are done.
        // Locks the revision thread.
        // After this, "Submit to Admin" becomes available.
        //
        // PATCH /api/samplestyle/{id}/clientapprove
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/clientapprove")]
        public async Task<ActionResult<SampleStyle>> MarkClientApproved(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (style.SubmittedToAdmin)
                return BadRequest("Cannot change client approval after submission to admin.");

            // Toggle — allows dev to undo if clicked by mistake (before submission)
            style.ClientApproved   = !style.ClientApproved;
            style.ClientApprovedAt = style.ClientApproved ? Now : null;
            style.ClientApprovedBy = style.ClientApproved ? CurrentUser : null;
            style.UpdatedAt        = Now;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ==========================================
        // SUBMIT TO ADMIN
        // Developer submits after client approval.
        // The latest revision comment + number are
        // already on the record — admin sees full history.
        // Requires: RC Meeting Date + Bulk Qty.
        //
        // PATCH /api/samplestyle/{id}/submit
        // Body: { "rcMeetingDate": "...", "bulkQty": "1000",
        //         "boardSet": "...", "acNumber": "...",
        //         "developerComments": "..." }
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/submit")]
        public async Task<ActionResult<SampleStyle>> SubmitToAdmin(
            string id, [FromBody] SubmitToAdminDto dto)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (!style.ClientApproved)
                return BadRequest("Style must be client-approved before submitting to admin.");
            if (style.SubmittedToAdmin)
                return BadRequest("Already submitted to admin.");
            if (string.IsNullOrWhiteSpace(dto.RcMeetingDate))
                return BadRequest("RC Meeting Date is required.");
            if (string.IsNullOrWhiteSpace(dto.BulkQty))
                return BadRequest("Bulk Qty is required.");

            style.RcMeetingDate      = dto.RcMeetingDate.Trim();
            style.BoardSet           = dto.BoardSet?.Trim();
            style.BulkQty            = dto.BulkQty.Trim();
            style.DeveloperComments  = dto.DeveloperComments?.Trim();
            style.SubmittedToAdmin   = true;
            style.SubmittedAt        = Now;
            style.AdminStatus        = "Pending";
            style.UpdatedAt          = Now;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ==========================================
        // ADMIN ACTION
        // Admin approves or rejects a submitted style.
        // On approval, bridges into Submissions + Approvals
        // so the rest of the pipeline (StoreIn, CPI etc.)
        // can see this style.
        //
        // PATCH /api/samplestyle/{id}/adminaction
        // Body: { "status": "Approved", "remarks": "..." }
        // ==========================================

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/adminaction")]
        public async Task<ActionResult<SampleStyle>> AdminAction(
            string id, [FromBody] AdminActionDto dto)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (!style.SubmittedToAdmin)
                return BadRequest("Style has not been submitted to admin yet.");

            // Lock guard: once StoreIn records exist, decision is frozen
            var hasStoreIn = await _context.StoreInRecords
                .AnyAsync(s => s.SubmissionId == style.Id);
            if (hasStoreIn)
                return BadRequest("LOCKED: Store-In records already exist for this style. " +
                                  "Approval cannot be changed once goods have been received.");

            var allowed = new[] { "Approved", "Pending" };
            if (!allowed.Contains(dto.Status))
                return BadRequest("Status must be 'Approved' or 'Pending'.");

            style.AdminStatus   = dto.Status;
            style.AdminRemarks  = dto.Remarks?.Trim();
            style.AdminActionAt = Now;
            style.AdminActionBy = CurrentUser;
            style.UpdatedAt     = Now;

            // ── Bridge: sync into Submissions + Approvals ─────────────────────
            // Build a meaningful comment using the latest revision
            var latestRevision = style.Revisions?
                .OrderByDescending(r => r.RevisionNo)
                .FirstOrDefault();

            var submissionComment = latestRevision != null
                ? $"Rev {latestRevision.RevisionNo}: {latestRevision.Comment}"
                : style.DeveloperComments ?? "Sample style approval";

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == style.Id);

            if (submission == null)
            {
                submission = new SubmissionForm
                {
                    Id               = style.Id,
                    StyleNo          = style.StyleNo,
                    CustomerName     = style.Customer,
                    SubmissionDate   = style.SubmittedAt ?? Now,
                    Level            = "Sample",
                    Comment          = submissionComment,
                    RevisionNo       = latestRevision?.RevisionNo ?? 1,
                    IsLatestRevision = true,
                };
                _context.Submissions.Add(submission);
            }

            var approval = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == style.Id);

            if (dto.Status == "Approved")
            {
                if (approval == null)
                {
                    _context.Approvals.Add(new ApprovalRecord
                    {
                        Id            = Guid.NewGuid().ToString(),
                        SubmissionId  = style.Id,
                        StyleNo       = style.StyleNo,
                        CustomerName  = style.Customer,
                        Level         = "Sample",
                        RevisionNo    = latestRevision?.RevisionNo ?? 1,
                        Status        = "Approved",
                        BoardSet      = style.BoardSet,
                        ApprovalCard  = style.AcNumber,
                        RaMeetingDate = style.RcMeetingDate,
                        BulkOrderQty  = style.BulkQty,
                        ReviewedAt    = Now,
                    });
                }
                else
                {
                    approval.Status        = "Approved";
                    approval.BoardSet      = style.BoardSet;
                    approval.ApprovalCard  = style.AcNumber;
                    approval.RaMeetingDate = style.RcMeetingDate;
                    approval.BulkOrderQty  = style.BulkQty;
                    approval.ReviewedAt    = Now;
                    approval.StyleNo       = style.StyleNo;
                    approval.CustomerName  = style.Customer;
                }
            }
            else if (approval != null)
            {
                approval.Status        = "Pending";
                approval.BoardSet      = null;
                approval.ApprovalCard  = null;
                approval.RaMeetingDate = null;
                approval.BulkOrderQty  = null;
                approval.ReviewedAt    = Now;
            }

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ==========================================
        // CREATE BULK REVISION (extra qty)
        // Used when client increases bulk qty after
        // the style is already approved.
        // Developer enters the EXTRA qty only.
        // System sums all revisions for total bulk.
        //
        // POST /api/samplestyle/{id}/revise
        // Body: { "extraBulkQty": "500", "rcMeetingDate": "...",
        //         "comments": "Second order from client" }
        // ==========================================

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("{id}/revise")]
        public async Task<ActionResult<SampleStyle>> CreateBulkRevision(
            string id, [FromBody] ReviseStyleDto dto)
        {
            var source = await _context.SampleStyles.FindAsync(id);
            if (source == null) return NotFound();

            if (source.AdminStatus != "Approved")
                return BadRequest("Only approved styles can be revised.");

            if (string.IsNullOrWhiteSpace(dto.ExtraBulkQty) ||
                !int.TryParse(dto.ExtraBulkQty, out var extraQty) || extraQty <= 0)
                return BadRequest("Extra Bulk Qty must be a positive number.");

            var existingCount = await _context.SampleStyles
                .CountAsync(s => s.StyleNo   == source.StyleNo
                              && s.Customer  == source.Customer
                              && s.Component == source.Component);

            var newStyle = new SampleStyle
            {
                Id                = Guid.NewGuid().ToString(),
                DevelopmentJobId  = source.DevelopmentJobId,
                Customer          = source.Customer,
                StyleNo           = source.StyleNo,
                Season            = source.Season,
                PrintingTechnique = source.PrintingTechnique,
                BodyColour        = source.BodyColour,
                PrintColour       = source.PrintColour,
                PrintColourQty    = source.PrintColourQty,
                WashingStandard   = source.WashingStandard,
                Component         = source.Component,
                ImagePath         = source.ImagePath,
                BulkQty           = dto.ExtraBulkQty.Trim(),
                RcMeetingDate     = dto.RcMeetingDate?.Trim() ?? source.RcMeetingDate,
                AcNumber          = dto.AcNumber?.Trim()      ?? source.AcNumber,
                BoardSet          = dto.BoardSet?.Trim()       ?? source.BoardSet,
                DeveloperComments = dto.Comments?.Trim(),
                Revisions         = new List<SampleStyleRevision>(),
                // Auto client-approved and submitted — style already approved
                ClientApproved    = true,
                ClientApprovedAt  = Now,
                ClientApprovedBy  = CurrentUser,
                SubmittedToAdmin  = true,
                SubmittedAt       = Now,
                AdminStatus       = "Pending",
                CreatedAt         = Now,
                UpdatedAt         = Now,
            };

            _context.SampleStyles.Add(newStyle);

            var revisionNo = existingCount + 1;
            _context.Submissions.Add(new SubmissionForm
            {
                Id               = newStyle.Id,
                StyleNo          = newStyle.StyleNo,
                CustomerName     = newStyle.Customer,
                SubmissionDate   = Now,
                Level            = "Sample",
                Comment          = $"Bulk revision {revisionNo} — Extra qty: {dto.ExtraBulkQty}",
                RevisionNo       = revisionNo,
                IsLatestRevision = false,
            });

            await _context.SaveChangesAsync();
            return Ok(newStyle);
        }

        // ==========================================
        // DELETE
        // ==========================================

        // ==========================================
        // SERVE SAMPLE IMAGE
        // GET /api/samplestyle/image?path=/uploads/samples/xyz.jpg
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
                ".png"            => "image/png",
                ".webp"           => "image/webp",
                _                 => "application/octet-stream",
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
                var filePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }

            _context.SampleStyles.Remove(style);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    // ==========================================
    // DTOs
    // ==========================================

    public class AddRevisionDto
    {
        public string Comment { get; set; } = string.Empty;
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
        /// <summary>Extra bulk qty only — added on top of existing approved qty.</summary>
        public string  ExtraBulkQty  { get; set; } = string.Empty;
        public string? RcMeetingDate { get; set; }
        public string? AcNumber      { get; set; }
        public string? BoardSet      { get; set; }
        public string? Comments      { get; set; }
    }

    public class AdminActionDto
    {
        public string  Status  { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}