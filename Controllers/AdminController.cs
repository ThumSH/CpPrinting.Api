using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/admin/approvals
        [HttpGet("approvals")]
        public async Task<ActionResult<IEnumerable<ApprovalRecord>>> GetApprovals()
        {
            return await _context.Approvals
                .OrderByDescending(a => a.ReviewedAt)
                .ThenByDescending(a => a.RevisionNo)
                .ToListAsync();
        }

        // POST: api/admin/approvals
        // Update if exists, insert if new
        [HttpPost("approvals")]
        public async Task<ActionResult<ApprovalRecord>> ProcessApproval(ApprovalRecord approval)
        {
            if (string.IsNullOrWhiteSpace(approval.SubmissionId))
                return BadRequest("SubmissionId is required.");

            if (string.IsNullOrWhiteSpace(approval.Status))
                return BadRequest("Status is required.");

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == approval.SubmissionId);

            if (submission == null)
                return BadRequest("Linked submission not found.");

            // IMPORTANT: only latest revision can be approved/edited
            if (!submission.IsLatestRevision)
                return BadRequest("Older revision approvals are locked. Only the latest revision can be processed.");

            approval.StyleNo = submission.StyleNo;
            approval.CustomerName = submission.CustomerName;
            approval.Level = submission.Level;
            approval.RevisionNo = submission.RevisionNo;

            var existing = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == approval.SubmissionId);

            if (existing != null)
            {
                existing.Status = approval.Status;
                existing.BoardSet = approval.Status == "Approved" ? approval.BoardSet : null;
                existing.ApprovalCard = approval.Status == "Approved" ? approval.ApprovalCard : null;
                existing.RaMeetingDate = approval.Status == "Approved" ? approval.RaMeetingDate : null;
                existing.BulkOrderQty = approval.Status == "Approved" ? approval.BulkOrderQty : null;
                existing.ReviewedAt = approval.ReviewedAt;
                existing.RevisionNo = submission.RevisionNo;
                existing.StyleNo = submission.StyleNo;
                existing.CustomerName = submission.CustomerName;
                existing.Level = submission.Level;

                _context.Entry(existing).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(existing);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(approval.Id))
                {
                    approval.Id = Guid.NewGuid().ToString();
                }

                if (approval.Status != "Approved")
                {
                    approval.BoardSet = null;
                    approval.ApprovalCard = null;
                    approval.RaMeetingDate = null;
                    approval.BulkOrderQty = null;
                }

                _context.Approvals.Add(approval);
                await _context.SaveChangesAsync();

                return Ok(approval);
            }
        }

        // DELETE: api/admin/approvals/{id}
        [HttpDelete("approvals/{id}")]
        public async Task<IActionResult> DeleteApproval(string id)
        {
            var approval = await _context.Approvals.FindAsync(id);
            if (approval == null)
                return NotFound();

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == approval.SubmissionId);

            if (submission == null)
                return BadRequest("Linked submission not found.");

            if (!submission.IsLatestRevision)
                return BadRequest("Older revision approvals are locked and cannot be deleted.");

            _context.Approvals.Remove(approval);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}