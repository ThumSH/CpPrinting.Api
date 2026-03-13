using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Admin")] // Strictly lock this down
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
            return await _context.Approvals.OrderByDescending(a => a.ReviewedAt).ToListAsync();
        }

        // POST: api/admin/approvals
        // This acts as an "Upsert" (Update if exists, Insert if new)
        [HttpPost("approvals")]
        public async Task<ActionResult<ApprovalRecord>> ProcessApproval(ApprovalRecord approval)
        {
            // Check if an approval for this specific submission already exists
            var existing = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == approval.SubmissionId);

            if (existing != null)
            {
                // Update the existing record
                existing.Status = approval.Status;
                existing.BoardSet = approval.BoardSet;
                existing.ApprovalCard = approval.ApprovalCard;
                existing.RaMeetingDate = approval.RaMeetingDate;
                existing.BulkOrderQty = approval.BulkOrderQty;
                existing.ReviewedAt = approval.ReviewedAt;

                _context.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                // Insert a brand new record
                _context.Approvals.Add(approval);
            }

            await _context.SaveChangesAsync();
            
            // Return the saved object back to React
            return Ok(approval);
        }
        
        // DELETE: api/admin/approvals/{id}
        [HttpDelete("approvals/{id}")]
        public async Task<IActionResult> DeleteApproval(string id)
        {
            var approval = await _context.Approvals.FindAsync(id);
            if (approval == null) return NotFound();

            _context.Approvals.Remove(approval);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}