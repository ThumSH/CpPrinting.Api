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

        public DevelopmentController(AppDbContext context)
        {
            _context = context;
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

        [Authorize(Roles = "Developer,Admin")]
        [HttpPost("jobs")]
        public async Task<ActionResult<DevelopmentJob>> CreateJob(DevelopmentJob job)
        {
            if (string.IsNullOrWhiteSpace(job.Id))
            {
                job.Id = Guid.NewGuid().ToString();
            }

            _context.DevelopmentJobs.Add(job);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJobs), new { id = job.Id }, job);
        }

        [Authorize(Roles = "Developer,Admin")]
        [HttpPut("jobs/{id}")]
        public async Task<IActionResult> UpdateJob(string id, DevelopmentJob job)
        {
            if (id != job.Id)
                return BadRequest("ID mismatch");

            _context.Entry(job).State = EntityState.Modified;

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

        [Authorize(Roles = "Developer,Admin")]
        [HttpDelete("jobs/{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var job = await _context.DevelopmentJobs.FindAsync(id);
            if (job == null)
                return NotFound();

            _context.DevelopmentJobs.Remove(job);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // SUBMISSIONS ENDPOINTS
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
            {
                submission.Id = Guid.NewGuid().ToString();
            }

            var matchingSubmissions = await _context.Submissions
                .Where(s =>
                    s.StyleNo.ToLower() == submission.StyleNo.ToLower() &&
                    s.CustomerName.ToLower() == submission.CustomerName.ToLower())
                .ToListAsync();

            foreach (var oldSubmission in matchingSubmissions.Where(s => s.IsLatestRevision))
            {
                oldSubmission.IsLatestRevision = false;
            }

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

        private bool JobExists(string id)
        {
            return _context.DevelopmentJobs.Any(e => e.Id == id);
        }
    }
}