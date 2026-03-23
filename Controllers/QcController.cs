using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.DTOs;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "QC,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class QcController : ControllerBase
    {
        private readonly AppDbContext _context;

        private static readonly string[] AllowedStatuses = { "Pending", "Passed", "Failed" };

        public QcController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // ELIGIBLE STORE-IN ITEMS FOR CPI
        // Now reads cut/bundle data from child tables
        // ==========================================

        [HttpGet("eligible-cpi")]
        public async Task<ActionResult<IEnumerable<EligibleCpiDto>>> GetEligibleCpiItems()
        {
            var existingCpiStoreInIds = await _context.CpiReports
                .Select(r => r.StoreInRecordId)
                .ToListAsync();

            var eligibleRecords = await _context.StoreInRecords
                .Include(r => r.Cuts)
                    .ThenInclude(c => c.Bundles)
                .Where(r => !existingCpiStoreInIds.Contains(r.Id))
                .OrderByDescending(r => r.CutInDate)
                .ToListAsync();

            var eligibleItems = eligibleRecords.Select(r =>
            {
                // Flatten cuts/bundles into summary fields for CPI display
                var firstCut = r.Cuts.FirstOrDefault();
                var firstBundle = firstCut?.Bundles.FirstOrDefault();

                return new EligibleCpiDto
                {
                    StoreInRecordId = r.Id,
                    SubmissionId = r.SubmissionId,
                    RevisionNo = r.RevisionNo,
                    StyleNo = r.StyleNo ?? string.Empty,
                    CustomerName = r.CustomerName ?? string.Empty,
                    ScheduleNo = r.ScheduleNo,
                    BodyColour = r.BodyColour ?? string.Empty,
                    PrintColour = r.PrintColour ?? string.Empty,
                    Components = r.Components ?? string.Empty,
                    Season = r.Season ?? string.Empty,
                    ReceivedQty = r.InQty,
                    CutInDate = r.CutInDate ?? string.Empty,
                    // Aggregate cut/bundle info from child tables
                    CutCount = r.Cuts.Count,
                    TotalCutQty = r.Cuts.Sum(c => c.CutQty),
                    TotalBundleCount = r.Cuts.Sum(c => c.Bundles.Count),
                    // For backward compat, provide first cut/bundle info
                    CutNo = firstCut?.CutNo ?? string.Empty,
                    Size = firstBundle?.Size ?? string.Empty,
                    BundleQty = firstBundle?.BundleQty ?? 0,
                    NumberRange = firstBundle?.NumberRange ?? string.Empty,
                    // Full cuts data for the CPI grid
                    Cuts = r.Cuts.Select(c => new CpiCutDto
                    {
                        CutNo = c.CutNo,
                        CutQty = c.CutQty,
                        Bundles = c.Bundles.Select(b => new CpiBundleDto
                        {
                            BundleNo = b.BundleNo,
                            BundleQty = b.BundleQty,
                            Size = b.Size,
                            NumberRange = b.NumberRange ?? string.Empty
                        }).ToList()
                    }).ToList()
                };
            }).ToList();

            return Ok(eligibleItems);
        }

        // ==========================================
        // CPI REPORTS
        // ==========================================

        [HttpGet("reports")]
        public async Task<ActionResult<IEnumerable<CPIReport>>> GetCPIReports()
        {
            return await _context.CpiReports
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("reports")]
        public async Task<ActionResult<CPIReport>> CreateCPIReport(CPIReport report)
        {
            if (string.IsNullOrWhiteSpace(report.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(report.Date))
                return BadRequest("Date is required.");

            if (!AllowedStatuses.Contains(report.InspectionStatus))
                return BadRequest("Invalid InspectionStatus. Allowed: Pending, Passed, Failed.");

            var storeInRecord = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == report.StoreInRecordId);

            if (storeInRecord == null)
                return BadRequest("Linked Store-In record not found.");

            var linkedApproval = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == storeInRecord.SubmissionId);

            if (linkedApproval == null || linkedApproval.Status != "Approved")
                return BadRequest("Only approved latest revisions from Stores can be inspected in CPI.");

            if (string.IsNullOrWhiteSpace(report.Id))
                report.Id = Guid.NewGuid().ToString();

            // Prevent duplicate CPI for same Store-In record
            var existingReport = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.StoreInRecordId == report.StoreInRecordId);

            if (existingReport != null)
                return BadRequest("A CPI report already exists for this Store-In record.");

            // Backend truth — all nullable-safe
            report.SubmissionId = storeInRecord.SubmissionId;
            report.RevisionNo = storeInRecord.RevisionNo;
            report.StyleNo = storeInRecord.StyleNo ?? string.Empty;
            report.Customer = storeInRecord.CustomerName ?? string.Empty;
            report.ScheduleNo = storeInRecord.ScheduleNo;
            report.BodyColour = storeInRecord.BodyColour ?? string.Empty;
            report.PrintColour = storeInRecord.PrintColour ?? string.Empty;
            report.ReceivedQty = storeInRecord.InQty;

            if (string.IsNullOrWhiteSpace(report.SummaryDate))
                report.SummaryDate = report.Date;

            _context.CpiReports.Add(report);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCPIReports), new { id = report.Id }, report);
        }

        [HttpPut("reports/{id}")]
        public async Task<IActionResult> UpdateCPIReport(string id, CPIReport report)
        {
            if (id != report.Id)
                return BadRequest("ID mismatch");

            if (!AllowedStatuses.Contains(report.InspectionStatus))
                return BadRequest("Invalid InspectionStatus. Allowed: Pending, Passed, Failed.");

            var existing = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existing == null)
                return NotFound();

            var storeInRecord = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == existing.StoreInRecordId);

            if (storeInRecord == null)
                return BadRequest("Linked Store-In record not found.");

            // Update editable fields
            // Guard: cannot revoke "Passed" status if production records already exist
            if (existing.InspectionStatus == "Passed" && report.InspectionStatus != "Passed")
            {
                var hasProduction = await _context.StoreProductionRecords
                    .AnyAsync(p => p.StoreInRecordId == existing.StoreInRecordId);

                if (hasProduction)
                    return BadRequest("Cannot change from Passed: production records already issued based on this QC pass.");
            }

            existing.Date = report.Date;
            existing.CpiQty = report.CpiQty;
            existing.CutInspections = report.CutInspections;
            existing.CuttingQty = report.CuttingQty;
            existing.CheckedQty = report.CheckedQty;
            existing.RejDamageQty = report.RejDamageQty;
            existing.RejectionPercentage = report.RejectionPercentage;
            existing.BalanceQty = report.BalanceQty;
            existing.InspectionStatus = report.InspectionStatus;
            existing.AppRej = report.AppRej;
            existing.CheckedBy = report.CheckedBy;
            existing.SummaryDate = report.SummaryDate;
            existing.CpiAuditor = report.CpiAuditor;

            // Re-apply backend truth (nullable-safe)
            existing.SubmissionId = storeInRecord.SubmissionId;
            existing.RevisionNo = storeInRecord.RevisionNo;
            existing.StyleNo = storeInRecord.StyleNo ?? string.Empty;
            existing.Customer = storeInRecord.CustomerName ?? string.Empty;
            existing.ScheduleNo = storeInRecord.ScheduleNo;
            existing.BodyColour = storeInRecord.BodyColour ?? string.Empty;
            existing.PrintColour = storeInRecord.PrintColour ?? string.Empty;
            existing.ReceivedQty = storeInRecord.InQty;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("reports/{id}")]
        public async Task<IActionResult> DeleteCPIReport(string id)
        {
            var report = await _context.CpiReports.FindAsync(id);
            if (report == null) return NotFound();

            // Block if production records exist for this store-in
            var hasProduction = await _context.StoreProductionRecords
                .AnyAsync(p => p.StoreInRecordId == report.StoreInRecordId);

            if (hasProduction)
                return BadRequest("Cannot delete: production records have been issued based on this QC pass.");

            // Block if advice notes exist
            var hasAdviceNotes = await _context.AdviceNotes
                .AnyAsync(a => a.StoreInRecordId == report.StoreInRecordId);

            if (hasAdviceNotes)
                return BadRequest("Cannot delete: Gatepass advice notes exist for this inspected item.");

            _context.CpiReports.Remove(report);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Returns which CPI reports have downstream dependencies.
        /// </summary>
        [HttpGet("reports/locks")]
        public async Task<ActionResult> GetCpiLocks()
        {
            var reports = await _context.CpiReports.Select(r => new { r.Id, r.StoreInRecordId }).ToListAsync();
            var prodStoreInIds = await _context.StoreProductionRecords.Select(p => p.StoreInRecordId).Distinct().ToListAsync();
            var gateStoreInIds = await _context.AdviceNotes.Select(a => a.StoreInRecordId).Distinct().ToListAsync();

            var locks = reports.ToDictionary(
                r => r.Id,
                r => new
                {
                    HasProduction = prodStoreInIds.Contains(r.StoreInRecordId),
                    HasGatepass = gateStoreInIds.Contains(r.StoreInRecordId),
                    IsLocked = prodStoreInIds.Contains(r.StoreInRecordId) || gateStoreInIds.Contains(r.StoreInRecordId)
                }
            );

            return Ok(locks);
        }
    }
}