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

        // ── GET all ───────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SampleStyle>>> GetAll(
            [FromQuery] string? customer = null,
            [FromQuery] string? styleNo = null,
            [FromQuery] string? adminStatus = null,
            [FromQuery] bool? clientApproved = null,
            [FromQuery] bool? submittedToAdmin = null)
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

        // ── CREATE ────────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost]
        public async Task<ActionResult<SampleStyle>> Create([FromBody] SampleStyle style)
        {
            style.Id = Guid.NewGuid().ToString();
            style.AdminStatus = "Pending";
            style.ClientApproved = false;
            style.SubmittedToAdmin = false;
            style.CreatedAt = Now;
            style.UpdatedAt = Now;

            _context.SampleStyles.Add(style);
            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── IMAGE UPLOAD ──────────────────────────────────────────────────────

        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("{id}/image")]
        [RequestSizeLimit(10_485_760)]
        public async Task<ActionResult<SampleStyle>> UploadImage(string id, IFormFile file)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            if (file.Length > MaxImageBytes) return BadRequest("File exceeds 10 MB limit.");
            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed.");

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "samples");
            Directory.CreateDirectory(uploadsDir);

            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var oldFile = Path.Combine(_env.WebRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream);

            style.ImagePath = $"/uploads/samples/{fileName}";
            style.UpdatedAt = Now;
            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── CLIENT APPROVE ────────────────────────────────────────────────────

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/clientapprove")]
        public async Task<ActionResult<SampleStyle>> ToggleClientApprove(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (style.SubmittedToAdmin)
                return BadRequest("Cannot change client approval after submission to admin.");

            style.ClientApproved = !style.ClientApproved;
            style.ClientApprovedAt = style.ClientApproved ? Now : null;
            style.ClientApprovedBy = style.ClientApproved ? CurrentUser : null;
            style.UpdatedAt = Now;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── SUBMIT TO ADMIN ───────────────────────────────────────────────────

        [Authorize(Roles = "Admin,Developer")]
        [HttpPatch("{id}/submit")]
        public async Task<ActionResult<SampleStyle>> SubmitToAdmin(string id, [FromBody] SubmitToAdminDto dto)
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

            style.RcMeetingDate = dto.RcMeetingDate.Trim();
            style.AcNumber = dto.AcNumber?.Trim();
            style.BoardSet = dto.BoardSet?.Trim();
            style.BulkQty = dto.BulkQty.Trim();
            style.DeveloperComments = dto.DeveloperComments?.Trim();
            style.SubmittedToAdmin = true;
            style.SubmittedAt = Now;
            style.AdminStatus = "Pending";
            style.UpdatedAt = Now;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── ADMIN ACTION + BRIDGE ─────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/adminaction")]
        public async Task<ActionResult<SampleStyle>> AdminAction(string id, [FromBody] AdminActionDto dto)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (!style.SubmittedToAdmin)
                return BadRequest("Style has not been submitted to admin yet.");

            var allowed = new[] { "Approved", "Pending" };
            if (!allowed.Contains(dto.Status))
                return BadRequest("Status must be 'Approved' or 'Pending'.");

            style.AdminStatus = dto.Status;
            style.AdminRemarks = dto.Remarks?.Trim();
            style.AdminActionAt = Now;
            style.AdminActionBy = CurrentUser;
            style.UpdatedAt = Now;

            // ── BRIDGE: sync to Submissions + Approvals ───────────────────────
            var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == style.Id);
            if (submission == null)
            {
                submission = new SubmissionForm
                {
                    Id = style.Id,
                    StyleNo = style.StyleNo,
                    CustomerName = style.Customer,
                    SubmissionDate = style.SubmittedAt ?? Now,
                    Level = "Sample",
                    Comment = style.DeveloperComments ?? "Sample style approval",
                    RevisionNo = 1,
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
                        Id = Guid.NewGuid().ToString(),
                        SubmissionId = style.Id,
                        StyleNo = style.StyleNo,
                        CustomerName = style.Customer,
                        Level = "Sample",
                        RevisionNo = 1,
                        Status = "Approved",
                        BoardSet = style.BoardSet,
                        ApprovalCard = style.AcNumber,
                        RaMeetingDate = style.RcMeetingDate,
                        BulkOrderQty = style.BulkQty,
                        ReviewedAt = Now,
                    });
                }
                else
                {
                    approval.Status = "Approved";
                    approval.BoardSet = style.BoardSet;
                    approval.ApprovalCard = style.AcNumber;
                    approval.RaMeetingDate = style.RcMeetingDate;
                    approval.BulkOrderQty = style.BulkQty;
                    approval.ReviewedAt = Now;
                    approval.StyleNo = style.StyleNo;
                    approval.CustomerName = style.Customer;
                }
            }
            else if (approval != null)
            {
                var hasStoreIn = await _context.StoreInRecords.AnyAsync(s => s.SubmissionId == style.Id);
                if (hasStoreIn)
                    return BadRequest("Cannot revert to Pending: Store-In records already exist for this style.");

                approval.Status = "Pending";
                approval.BoardSet = null;
                approval.ApprovalCard = null;
                approval.RaMeetingDate = null;
                approval.BulkOrderQty = null;
                approval.ReviewedAt = Now;
            }

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var filePath = Path.Combine(_env.WebRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }

            _context.SampleStyles.Remove(style);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class SubmitToAdminDto
    {
        public string RcMeetingDate { get; set; } = string.Empty;
        public string? AcNumber { get; set; }
        public string? BoardSet { get; set; }
        public string BulkQty { get; set; } = string.Empty;
        public string? DeveloperComments { get; set; }
    }

    public class AdminActionDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}