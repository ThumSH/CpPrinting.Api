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

        // Images are saved under wwwroot/uploads/samples/
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];
        private const long MaxImageBytes = 10 * 1024 * 1024; // 10 MB

        public SampleStyleController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private string CurrentUser =>
            User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

        private string Now =>
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        // ── GET all ───────────────────────────────────────────────────────────

        /// <summary>
        /// GET api/samplestyle
        /// Admin  → all records
        /// Developer → only their own customer/style records (all records for now;
        ///             filter by username can be added later)
        /// </summary>
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

            var results = await query
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>
        /// GET api/samplestyle/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SampleStyle>> GetById(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();
            return Ok(style);
        }

        // ── CREATE (called automatically from DevelopmentController) ──────────

        /// <summary>
        /// POST api/samplestyle
        /// Called internally when a DevelopmentJob is created.
        /// Developer role required.
        /// </summary>
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

        /// <summary>
        /// POST api/samplestyle/{id}/image
        /// Accepts multipart/form-data with a single "file" field.
        /// Saves to wwwroot/uploads/samples/ and updates ImagePath.
        /// Developer role required.
        /// </summary>
        [Authorize(Roles = "Admin,Developer")]
        [HttpPost("{id}/image")]
        [RequestSizeLimit(10_485_760)] // 10 MB
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

            // Build save path
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "samples");
            Directory.CreateDirectory(uploadsDir);

            // Delete old image if one exists
            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var oldFile = Path.Combine(_env.WebRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFile))
                    System.IO.File.Delete(oldFile);
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            // Store as a relative URL the frontend can use directly
            style.ImagePath = $"/uploads/samples/{fileName}";
            style.UpdatedAt = Now;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── DEVELOPER: mark client approved ───────────────────────────────────

        /// <summary>
        /// PATCH api/samplestyle/{id}/clientapprove
        /// Developer toggles "Client Approved" status.
        /// Once true, the style can be submitted to admin.
        /// </summary>
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

        // ── DEVELOPER: submit to admin ────────────────────────────────────────

        /// <summary>
        /// PATCH api/samplestyle/{id}/submit
        /// Developer fills in RC Meeting Date, AC, Board Set, Bulk Qty
        /// and submits for admin review. Requires ClientApproved = true.
        /// Body: { rcMeetingDate, acNumber, boardSet, bulkQty }
        /// </summary>
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
            style.SubmittedToAdmin = true;
            style.SubmittedAt = Now;
            style.AdminStatus = "Pending";
            style.UpdatedAt = Now;

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── ADMIN: set approval status ────────────────────────────────────────

        /// <summary>
        /// PATCH api/samplestyle/{id}/adminaction
        /// Admin sets status to "Approved" or "Pending" (not Rejected — per spec).
        /// Body: { status: "Approved" | "Pending", remarks?: string }
        /// </summary>
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

            await _context.SaveChangesAsync();
            return Ok(style);
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        /// <summary>
        /// DELETE api/samplestyle/{id}
        /// Admin only — also removes the associated image file.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var style = await _context.SampleStyles.FindAsync(id);
            if (style == null) return NotFound();

            // Delete image file if present
            if (!string.IsNullOrEmpty(style.ImagePath))
            {
                var filePath = Path.Combine(_env.WebRootPath, style.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
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
    }

    public class AdminActionDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}