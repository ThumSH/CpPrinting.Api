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
    public class DevelopmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DevelopmentController(AppDbContext context)
        {
            _context = context;
        }

        private string Now => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

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
        /// Creates a DevelopmentJob AND automatically creates a linked SampleStyle.
        /// Returns: { job, sampleStyle }
        /// </summary>
        [Authorize(Roles = "Developer,Admin")]
        [HttpPost("jobs")]
        public async Task<ActionResult<object>> CreateJob(DevelopmentJob job)
        {
            if (string.IsNullOrWhiteSpace(job.Id))
                job.Id = Guid.NewGuid().ToString();

            _context.DevelopmentJobs.Add(job);

            // Auto-create a linked SampleStyle
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
                Placements = job.Placements != null ? string.Join(",", job.Placements) : string.Empty,
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

        /// <summary>
        /// Updates the DevelopmentJob. Also syncs key fields to the linked
        /// SampleStyle if it hasn't been submitted to admin yet.
        /// </summary>
        [Authorize(Roles = "Developer,Admin")]
        [HttpPut("jobs/{id}")]
        public async Task<IActionResult> UpdateJob(string id, DevelopmentJob job)
        {
            if (id != job.Id)
                return BadRequest("ID mismatch");

            _context.Entry(job).State = EntityState.Modified;

            // Sync fields back to unsubmitted SampleStyle
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
                linked.Placements = job.Placements != null ? string.Join(",", job.Placements) : string.Empty;
                linked.UpdatedAt = Now;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobExists(id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes a job and its unsubmitted SampleStyle (if any).
        /// Submitted SampleStyles are kept — they're part of the admin record.
        /// </summary>
        [Authorize(Roles = "Developer,Admin")]
        [HttpDelete("jobs/{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var job = await _context.DevelopmentJobs.FindAsync(id);
            if (job == null)
                return NotFound();

            // Remove unsubmitted sample styles linked to this job
            var linkedStyles = await _context.SampleStyles
                .Where(s => s.DevelopmentJobId == id && !s.SubmittedToAdmin)
                .ToListAsync();

            _context.SampleStyles.RemoveRange(linkedStyles);
            _context.DevelopmentJobs.Remove(job);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // SUBMISSIONS ENDPOINTS — unchanged from original
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
                ? matchingSubmissions.Max(s => s.RevisionNo) + 1
                : 1;

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
            if (submission == null)
                return NotFound();

            bool wasLatest = submission.IsLatestRevision;
            string styleNo = submission.StyleNo;
            string customerName = submission.CustomerName;

            _context.Submissions.Remove(submission);
            await _context.SaveChangesAsync();

            if (wasLatest)
            {
                var previousRevision = await _context.Submissions
                    .Where(s =>
                        s.StyleNo.ToLower() == styleNo.ToLower() &&
                        s.CustomerName.ToLower() == customerName.ToLower())
                    .OrderByDescending(s => s.RevisionNo)
                    .FirstOrDefaultAsync();

                if (previousRevision != null)
                {
                    previousRevision.IsLatestRevision = true;
                    await _context.SaveChangesAsync();
                }
            }

            return NoContent();
        }

        private bool JobExists(string id) =>
            _context.DevelopmentJobs.Any(e => e.Id == id);
    }
}