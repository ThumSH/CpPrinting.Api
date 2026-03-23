using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public AdminController(AppDbContext context, ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
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

            if (!submission.IsLatestRevision)
                return BadRequest("Older revision approvals are locked. Only the latest revision can be processed.");

            approval.StyleNo = submission.StyleNo;
            approval.CustomerName = submission.CustomerName;
            approval.Level = submission.Level;
            approval.RevisionNo = submission.RevisionNo;

            // Validate BulkOrderQty if approving
            if (approval.Status == "Approved")
            {
                if (string.IsNullOrWhiteSpace(approval.BulkOrderQty) || !int.TryParse(approval.BulkOrderQty.Trim(), out var bqCheck) || bqCheck <= 0)
                    return BadRequest("Bulk Order Qty must be a valid positive number.");
            }

            var existing = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == approval.SubmissionId);

            if (existing != null)
            {
                // GUARD: If existing was "Approved" and new status is not "Approved",
                // check if any store-in records exist for this submission
                if (existing.Status == "Approved" && approval.Status != "Approved")
                {
                    var hasStoreIn = await _context.StoreInRecords
                        .AnyAsync(s => s.SubmissionId == approval.SubmissionId);

                    if (hasStoreIn)
                        return BadRequest("Cannot revoke approval: Store-In records already exist for this style. " +
                                          "Delete all store-in records first before changing the approval status.");
                }

                // GUARD: If reducing bulk qty, ensure it doesn't go below what's already received
                if (approval.Status == "Approved" && existing.Status == "Approved" &&
                    !string.IsNullOrWhiteSpace(approval.BulkOrderQty))
                {
                    var newBulk = int.TryParse(approval.BulkOrderQty, out var nb) ? nb : 0;
                    var totalInQty = await _context.StoreInRecords
                        .Where(s => s.SubmissionId == approval.SubmissionId)
                        .SumAsync(s => s.InQty);

                    if (newBulk < totalInQty)
                        return BadRequest($"Cannot reduce bulk qty to {newBulk}: already received {totalInQty} in Store-In.");
                }

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

                await _logger.Log(User, HttpContext, "Update", "Approval", existing.Id,
                    $"{existing.Status} style {existing.StyleNo} for {existing.CustomerName}" +
                    (existing.Status == "Approved" ? $" — Bulk: {existing.BulkOrderQty}" : ""));

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

                await _logger.Log(User, HttpContext, "Create", "Approval", approval.Id,
                    $"{approval.Status} style {approval.StyleNo} for {approval.CustomerName}" +
                    (approval.Status == "Approved" ? $" — Bulk: {approval.BulkOrderQty}" : ""));

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

            // GUARD: Block deletion if store-in records exist
            var hasStoreIn = await _context.StoreInRecords
                .AnyAsync(s => s.SubmissionId == approval.SubmissionId);

            if (hasStoreIn)
                return BadRequest("Cannot delete approval: Store-In records already exist for this style. " +
                                  "Delete all store-in records first.");

            _context.Approvals.Remove(approval);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "Approval", id,
                $"Deleted approval for {approval.StyleNo} ({approval.CustomerName})");

            return NoContent();
        }
    }
}