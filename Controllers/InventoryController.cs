using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.DTOs;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Stores,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InventoryController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // ELIGIBLE STYLES FOR STORE IN
        // ==========================================

        [HttpGet("eligible-store-in")]
        public async Task<ActionResult<IEnumerable<EligibleStoreInDto>>> GetEligibleStoreInStyles()
        {
            var rawEligibleStyles = await (
                from submission in _context.Submissions
                join approval in _context.Approvals
                    on submission.Id equals approval.SubmissionId
                join job in _context.DevelopmentJobs
                    on new
                    {
                        StyleNo = submission.StyleNo.ToLower(),
                        Customer = submission.CustomerName.ToLower()
                    }
                    equals new
                    {
                        StyleNo = job.StyleNo.ToLower(),
                        Customer = job.Customer.ToLower()
                    }
                where submission.IsLatestRevision == true
                      && approval.Status == "Approved"
                orderby approval.ReviewedAt descending
                select new
                {
                    submission.Id,
                    submission.RevisionNo,
                    submission.StyleNo,
                    submission.CustomerName,
                    submission.SubmissionDate,
                    submission.Level,
                    ApprovalStatus = approval.Status,
                    approval.ReviewedAt,
                    job.BodyColour,
                    job.PrintColour,
                    job.Season,
                    Components = string.Join(", ", job.Placements),
                    approval.BulkOrderQty
                }
            ).ToListAsync();

            var eligibleStyles = rawEligibleStyles.Select(x => new EligibleStoreInDto
            {
                SubmissionId = x.Id,
                RevisionNo = x.RevisionNo,
                StyleNo = x.StyleNo,
                CustomerName = x.CustomerName,
                SubmissionDate = x.SubmissionDate,
                Level = x.Level,
                ApprovalStatus = x.ApprovalStatus,
                ReviewedAt = x.ReviewedAt,
                BodyColour = x.BodyColour,
                PrintColour = x.PrintColour,
                Season = x.Season,
                Components = x.Components,
                ApprovedBulkQty = int.TryParse(x.BulkOrderQty, out var qty) ? qty : 0
            }).ToList();

            return Ok(eligibleStyles);
        }

        // ==========================================
        // STORE IN RECORDS
        // ==========================================

        [HttpGet("store-in")]
        public async Task<ActionResult<IEnumerable<StoreInRecord>>> GetStoreInRecords()
        {
            return await _context.StoreInRecords
                .OrderByDescending(r => r.CutInDate)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("store-in")]
        public async Task<ActionResult<StoreInRecord>> CreateStoreInRecord(StoreInRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.SubmissionId))
                return BadRequest("SubmissionId is required.");

            if (string.IsNullOrWhiteSpace(record.ScheduleNo))
                return BadRequest("ScheduleNo is required.");

            if (string.IsNullOrWhiteSpace(record.CutNo))
                return BadRequest("CutNo is required.");

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == record.SubmissionId);

            if (submission == null)
                return BadRequest("Linked submission not found.");

            if (!submission.IsLatestRevision)
                return BadRequest("Only the latest approved revision can move to Stores.");

            var approval = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == record.SubmissionId);

            if (approval == null)
                return BadRequest("This submission has not been approved yet.");

            if (approval.Status != "Approved")
                return BadRequest("Only approved revisions can move to Stores.");

            var matchingJob = await _context.DevelopmentJobs
                .FirstOrDefaultAsync(j =>
                    j.StyleNo.ToLower() == submission.StyleNo.ToLower() &&
                    j.Customer.ToLower() == submission.CustomerName.ToLower());

            if (matchingJob == null)
                return BadRequest("Matching development job not found.");

            if (string.IsNullOrWhiteSpace(record.Id))
            {
                record.Id = Guid.NewGuid().ToString();
            }

            record.StyleNo = submission.StyleNo;
            record.CustomerName = submission.CustomerName;
            record.RevisionNo = submission.RevisionNo;
            record.Season = matchingJob.Season;
            record.BodyColour = matchingJob.BodyColour;
            record.PrintColour = matchingJob.PrintColour;
            record.Components = string.Join(", ", matchingJob.Placements);

            if (record.BulkQty <= 0 && int.TryParse(approval.BulkOrderQty, out var approvedQty))
            {
                record.BulkQty = approvedQty;
            }

            _context.StoreInRecords.Add(record);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStoreInRecords), new { id = record.Id }, record);
        }

        [HttpPut("store-in/{id}")]
        public async Task<IActionResult> UpdateStoreInRecord(string id, StoreInRecord record)
        {
            if (id != record.Id)
                return BadRequest("ID mismatch.");

            var existing = await _context.StoreInRecords.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.ScheduleNo = record.ScheduleNo;
            existing.CutNo = record.CutNo;
            existing.CutInDate = record.CutInDate;
            existing.BulkQty = record.BulkQty;
            existing.InQty = record.InQty;
            existing.BalanceBulkQty = record.BalanceBulkQty;
            existing.CutQty = record.CutQty;
            existing.AvailableQty = record.AvailableQty;
            existing.BundleQty = record.BundleQty;
            existing.NumberRange = record.NumberRange;
            existing.Size = record.Size;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("store-in/{id}")]
        public async Task<IActionResult> DeleteStoreInRecord(string id)
        {
            var record = await _context.StoreInRecords.FindAsync(id);
            if (record == null) return NotFound();

            _context.StoreInRecords.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==========================================
        // ELIGIBLE ITEMS FOR PRODUCTION
        // ==========================================

        [HttpGet("eligible-production")]
        public async Task<ActionResult<IEnumerable<EligibleProductionDto>>> GetEligibleProductionItems()
        {
            var eligibleItems = await (
                from storeIn in _context.StoreInRecords
                join cpi in _context.CpiReports
                    on storeIn.Id equals cpi.StoreInRecordId
                where cpi.InspectionStatus == "Passed"
                      && storeIn.AvailableQty > 0
                orderby cpi.SummaryDate descending
                select new EligibleProductionDto
                {
                    StoreInRecordId = storeIn.Id,
                    SubmissionId = storeIn.SubmissionId,
                    RevisionNo = storeIn.RevisionNo,
                    StyleNo = storeIn.StyleNo,
                    CustomerName = storeIn.CustomerName,
                    Components = storeIn.Components,
                    CutNo = storeIn.CutNo,
                    AvailableQty = storeIn.AvailableQty,
                    InspectionStatus = cpi.InspectionStatus,
                    CheckedBy = cpi.CheckedBy,
                    SummaryDate = cpi.SummaryDate
                }
            ).ToListAsync();

            return Ok(eligibleItems);
        }

        // ==========================================
        // PRODUCTION ISSUES
        // ==========================================

        [HttpGet("production")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> GetProductionRecords()
        {
            return await _context.StoreProductionRecords
                .OrderByDescending(r => r.IssueDate)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("production")]
        public async Task<ActionResult<StoreProductionRecord>> CreateProductionRecord(StoreProductionRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(record.LineNo))
                return BadRequest("LineNo is required.");

            if (record.IssueQty <= 0)
                return BadRequest("IssueQty must be greater than zero.");

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            var cpi = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);

            if (cpi == null)
                return BadRequest("This item has not been inspected by QC yet.");

            if (cpi.InspectionStatus != "Passed")
                return BadRequest("Only QC-passed items can move to Production.");

            if (record.IssueQty > storeIn.AvailableQty)
                return BadRequest($"IssueQty exceeds available shelf stock ({storeIn.AvailableQty}).");

            if (string.IsNullOrWhiteSpace(record.Id))
            {
                record.Id = Guid.NewGuid().ToString();
            }

            record.SubmissionId = storeIn.SubmissionId;
            record.RevisionNo = storeIn.RevisionNo;
            record.StyleNo = storeIn.StyleNo;
            record.CustomerName = storeIn.CustomerName;
            record.Components = storeIn.Components;
            record.CutNo = storeIn.CutNo;
            record.BalanceQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);

            // Reduce store shelf availability after issuing
            storeIn.AvailableQty = record.BalanceQty;

            _context.StoreProductionRecords.Add(record);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductionRecords), new { id = record.Id }, record);
        }

        [HttpPut("production/{id}")]
        public async Task<IActionResult> UpdateProductionRecord(string id, StoreProductionRecord record)
        {
            if (id != record.Id)
                return BadRequest("ID mismatch.");

            var existing = await _context.StoreProductionRecords.FindAsync(id);
            if (existing == null)
                return NotFound();

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == existing.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            var cpi = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.StoreInRecordId == existing.StoreInRecordId);

            if (cpi == null || cpi.InspectionStatus != "Passed")
                return BadRequest("Only QC-passed items can remain in Production.");

            // Restore previous issued qty first
            storeIn.AvailableQty += existing.IssueQty;

            if (record.IssueQty <= 0)
                return BadRequest("IssueQty must be greater than zero.");

            if (record.IssueQty > storeIn.AvailableQty)
                return BadRequest($"Updated IssueQty exceeds available shelf stock ({storeIn.AvailableQty}).");

            existing.IssueDate = record.IssueDate;
            existing.IssueQty = record.IssueQty;
            existing.LineNo = record.LineNo;
            existing.BalanceQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);

            storeIn.AvailableQty = existing.BalanceQty;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("production/{id}")]
        public async Task<IActionResult> DeleteProductionRecord(string id)
        {
            var record = await _context.StoreProductionRecords.FindAsync(id);
            if (record == null) return NotFound();

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);

            if (storeIn != null)
            {
                storeIn.AvailableQty += record.IssueQty;
            }

            _context.StoreProductionRecords.Remove(record);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}