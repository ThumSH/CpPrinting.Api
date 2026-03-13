using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize] // Locks down all endpoints in this controller
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

        // GET: api/development/jobs
        [HttpGet("jobs")]
        public async Task<ActionResult<IEnumerable<DevelopmentJob>>> GetJobs()
        {
            return await _context.DevelopmentJobs.OrderByDescending(j => j.Id).ToListAsync();
        }

        // POST: api/development/jobs
        // Only Developers and Admins should be creating jobs
        [Authorize(Roles = "Developer,Admin")] 
        [HttpPost("jobs")]
        public async Task<ActionResult<DevelopmentJob>> CreateJob(DevelopmentJob job)
        {
            // If the frontend generates the ID, we use it; otherwise EF Core can generate one
            _context.DevelopmentJobs.Add(job);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJobs), new { id = job.Id }, job);
        }

        // PUT: api/development/jobs/{id}
        [Authorize(Roles = "Developer,Admin")]
        [HttpPut("jobs/{id}")]
        public async Task<IActionResult> UpdateJob(string id, DevelopmentJob job)
        {
            if (id != job.Id) return BadRequest("ID mismatch");

            _context.Entry(job).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/development/jobs/{id}
        [Authorize(Roles = "Developer,Admin")]
        [HttpDelete("jobs/{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var job = await _context.DevelopmentJobs.FindAsync(id);
            if (job == null) return NotFound();

            _context.DevelopmentJobs.Remove(job);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        // ==========================================
        // SUBMISSIONS ENDPOINTS
        // ==========================================

        // GET: api/development/submissions
        [HttpGet("submissions")]
        public async Task<ActionResult<IEnumerable<SubmissionForm>>> GetSubmissions()
        {
            return await _context.Submissions.OrderByDescending(s => s.SubmissionDate).ToListAsync();
        }

        // POST: api/development/submissions
        [Authorize(Roles = "Developer,Admin")]
        [HttpPost("submissions")]
        public async Task<ActionResult<SubmissionForm>> CreateSubmission(SubmissionForm submission)
        {
            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSubmissions), new { id = submission.Id }, submission);
        }
        
        // DELETE: api/development/submissions/{id}
        [Authorize(Roles = "Developer,Admin,QC")]
        [HttpDelete("submissions/{id}")]
        public async Task<IActionResult> DeleteSubmission(string id)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission == null) return NotFound();

            _context.Submissions.Remove(submission);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool JobExists(string id)
        {
            return _context.DevelopmentJobs.Any(e => e.Id == id);
        }
    }
}