using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DevelopmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
        private const long MaxImageBytes = 10 * 1024 * 1024;

        public DevelopmentController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private string Now => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        // ==========================================
        // ARTWORK UPLOAD
        // POST api/development/artwork
        // Saves to wwwroot/uploads/artworks/ — returns the server URL path.
        // Called from frontend BEFORE submitting the job form so we get a
        // real persistent URL instead of a blob:// that dies on reload.
        // ==========================================

        [Authorize(Roles = "Developer,Admin")]
        [HttpPost("artwork")]
        [RequestSizeLimit(10_485_760)]
        public async Task<ActionResult> UploadArtwork(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (file.Length > MaxImageBytes)
                return BadRequest("File exceeds 10 MB limit.");

            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed.");

            var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "artworks");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"artwork_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream);

            // Return full absolute URL so it works as a plain <img src> everywhere
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return Ok(new { url = $"{baseUrl}/uploads/artworks/{fileName}" });
        }

        // ==========================================
        // WORKSPACE JOBS ENDPOINTS
        // ==========================================

        [HttpGet("jobs")]
        public async Task<ActionResult<IEnumerable<DevelopmentJob>>> GetJobs()
        {
            return await _context.DevelopmentJobs
                .OrderByDescending(j => j.Id)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a DevelopmentJob AND a linked SampleStyle.
        /// ArtworkPreviewUrl must be a server path from POST /artwork (not a blob URL).
        /// Returns: { job, sampleStyle }
        /// </summary>
        [Authorize(Roles = "Developer,Admin")]
        [HttpPost("jobs")]
        public async Task<ActionResult<object>> CreateJob(DevelopmentJob job)
        {
            if (string.IsNullOrWhiteSpace(job.Id))
                job.Id = Guid.NewGuid().ToString();

            _context.DevelopmentJobs.Add(job);

            var sampleStyle = new SampleStyle
            {
                Id = Guid.NewGuid().ToString(),
                DevelopmentJobId = job.Id,
                Customer = job.Customer,
                StyleNo = job.StyleNo,
                Season = job.Season,
                PrintingTechnique = job.PrintingTechnique,
                BodyColour = job.BodyColour,
                PrintColour = job.PrintColour,
                PrintColourQty = job.PrintColourQty,
                WashingStandard = job.WashingStandard,
                Component = job.Component ?? string.Empty,
                // Artwork URL flows directly into SampleStyle.ImagePath
                ImagePath = !string.IsNullOrWhiteSpace(job.ArtworkPreviewUrl) ? job.ArtworkPreviewUrl : null,
                ClientApproved = false,
                SubmittedToAdmin = false,
                AdminStatus = "Pending",
                CreatedAt = Now,
                UpdatedAt = Now,
            };

            _context.SampleStyles.Add(sampleStyle);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJobs), new { id = job.Id }, new { job, sampleStyle });
        }

        [Authorize(Roles = "Developer,Admin")]
        [HttpPut("jobs/{id}")]
        public async Task<IActionResult> UpdateJob(string id, DevelopmentJob job)
        {
            if (id != job.Id)
                return BadRequest("ID mismatch");

            _context.Entry(job).State = EntityState.Modified;

            var linked = await _context.SampleStyles
                .FirstOrDefaultAsync(s => s.DevelopmentJobId == id && !s.SubmittedToAdmin);

            if (linked != null)
            {
                linked.Customer = job.Customer;
                linked.StyleNo = job.StyleNo;
                linked.Season = job.Season;
                linked.PrintingTechnique = job.PrintingTechnique;
                linked.BodyColour = job.BodyColour;
                linked.PrintColour = job.PrintColour;
                linked.PrintColourQty = job.PrintColourQty;
                linked.WashingStandard = job.WashingStandard;
                linked.Component = job.Component ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(job.ArtworkPreviewUrl))
                    linked.ImagePath = job.ArtworkPreviewUrl;
                linked.UpdatedAt = Now;
            }

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [Authorize(Roles = "Developer,Admin")]
        [HttpDelete("jobs/{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var job = await _context.DevelopmentJobs.FindAsync(id);
            if (job == null) return NotFound();

            var linkedStyles = await _context.SampleStyles
                .Where(s => s.DevelopmentJobId == id && !s.SubmittedToAdmin)
                .ToListAsync();

            _context.SampleStyles.RemoveRange(linkedStyles);
            _context.DevelopmentJobs.Remove(job);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // SUBMISSIONS ENDPOINTS — unchanged
        // ==========================================

        [HttpGet("submissions")]
        public async Task<ActionResult<IEnumerable<SubmissionForm>>> GetSubmissions()
        {
            return await _context.Submissions
                .OrderByDescending(s => s.SubmissionDate)
                .ThenByDescending(s => s.RevisionNo)
                .ToListAsync();
        }

        [Authorize(Roles = "Developer,Admin")]
        [HttpPost("submissions")]
        public async Task<ActionResult<SubmissionForm>> CreateSubmission(SubmissionForm submission)
        {
            if (string.IsNullOrWhiteSpace(submission.StyleNo))
                return BadRequest("Style No is required.");
            if (string.IsNullOrWhiteSpace(submission.CustomerName))
                return BadRequest("Customer Name is required.");
            if (string.IsNullOrWhiteSpace(submission.SubmissionDate))
                return BadRequest("Submission Date is required.");
            if (string.IsNullOrWhiteSpace(submission.Level))
                return BadRequest("Level is required.");
            if (string.IsNullOrWhiteSpace(submission.Comment))
                return BadRequest("Comment is required.");
            if (string.IsNullOrWhiteSpace(submission.Id))
                submission.Id = Guid.NewGuid().ToString();

            var matchingSubmissions = await _context.Submissions
                .Where(s =>
                    s.StyleNo.ToLower() == submission.StyleNo.ToLower() &&
                    s.CustomerName.ToLower() == submission.CustomerName.ToLower())
                .ToListAsync();

            foreach (var old in matchingSubmissions.Where(s => s.IsLatestRevision))
                old.IsLatestRevision = false;

            submission.RevisionNo = matchingSubmissions.Any()
                ? matchingSubmissions.Max(s => s.RevisionNo) + 1 : 1;
            submission.IsLatestRevision = true;

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSubmissions), new { id = submission.Id }, submission);
        }

        [Authorize(Roles = "Developer,Admin,QC")]
        [HttpDelete("submissions/{id}")]
        public async Task<IActionResult> DeleteSubmission(string id)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission == null) return NotFound();

            bool wasLatest = submission.IsLatestRevision;
            string styleNo = submission.StyleNo;
            string customerName = submission.CustomerName;

            _context.Submissions.Remove(submission);
            await _context.SaveChangesAsync();

            if (wasLatest)
            {
                var prev = await _context.Submissions
                    .Where(s =>
                        s.StyleNo.ToLower() == styleNo.ToLower() &&
                        s.CustomerName.ToLower() == customerName.ToLower())
                    .OrderByDescending(s => s.RevisionNo)
                    .FirstOrDefaultAsync();

                if (prev != null) { prev.IsLatestRevision = true; await _context.SaveChangesAsync(); }
            }

            return NoContent();
        }

        // ==========================================
        // SERVE ARTWORK IMAGE
        // GET /api/development/image?path=/uploads/artworks/xyz.jpg
        // Needed because UseStaticFiles is not configured in Program.cs
        // ==========================================

        [HttpGet("image")]
        public IActionResult GetImage([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/uploads/"))
                return BadRequest("Invalid path.");

            var root     = _env.WebRootPath ?? _env.ContentRootPath;
            var filePath = Path.Combine(root, path.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var ct  = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".webp"           => "image/webp",
                ".gif"            => "image/gif",
                _                 => "application/octet-stream",
            };
            return PhysicalFile(filePath, ct);
        }

        private bool JobExists(string id) =>
            _context.DevelopmentJobs.Any(e => e.Id == id);
    }
}