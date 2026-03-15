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
        // ==========================================

        [HttpGet("eligible-cpi")]
        public async Task<ActionResult<IEnumerable<EligibleCpiDto>>> GetEligibleCpiItems()
        {
            var existingCpiStoreInIds = await _context.CpiReports
                .Select(r => r.StoreInRecordId)
                .ToListAsync();

            var eligibleItems = await _context.StoreInRecords
                .Where(r => !existingCpiStoreInIds.Contains(r.Id))
                .OrderByDescending(r => r.CutInDate)
                .Select(r => new EligibleCpiDto
                {
                    StoreInRecordId = r.Id,
                    SubmissionId = r.SubmissionId,
                    RevisionNo = r.RevisionNo,
                    StyleNo = r.StyleNo,
                    CustomerName = r.CustomerName,
                    ScheduleNo = r.ScheduleNo,
                    CutNo = r.CutNo,
                    BodyColour = r.BodyColour,
                    PrintColour = r.PrintColour,
                    Components = r.Components,
                    Season = r.Season,
                    ReceivedQty = r.InQty,
                    CutInDate = r.CutInDate,
                    Size = r.Size,
                    BundleQty = r.BundleQty,
                    NumberRange = r.NumberRange
                })
                .ToListAsync();

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
            {
                report.Id = Guid.NewGuid().ToString();
            }

            // Prevent duplicate CPI for same Store-In record
            var existingReport = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.StoreInRecordId == report.StoreInRecordId);

            if (existingReport != null)
                return BadRequest("A CPI report already exists for this Store-In record.");

            // Backend truth
            report.SubmissionId = storeInRecord.SubmissionId;
            report.RevisionNo = storeInRecord.RevisionNo;
            report.StyleNo = storeInRecord.StyleNo;
            report.Customer = storeInRecord.CustomerName;
            report.ScheduleNo = storeInRecord.ScheduleNo;
            report.BodyColour = storeInRecord.BodyColour;
            report.PrintColour = storeInRecord.PrintColour;
            report.ReceivedQty = storeInRecord.InQty;

            // If not set, keep summary date aligned
            if (string.IsNullOrWhiteSpace(report.SummaryDate))
            {
                report.SummaryDate = report.Date;
            }

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

            // Keep trusted linkage fields locked
            existing.Date = report.Date;
            existing.CpiQty = report.CpiQty;
            existing.InspectionRows = report.InspectionRows;
            existing.CuttingQty = report.CuttingQty;
            existing.CheckedQty = report.CheckedQty;
            existing.RejDamageQty = report.RejDamageQty;
            existing.RejectionPercentage = report.RejectionPercentage;
            existing.BalanceQty = report.BalanceQty;
            existing.InspectionStatus = report.InspectionStatus;
            existing.AppRej = report.AppRej;
            existing.CheckedBy = report.CheckedBy;
            existing.SummaryDate = report.SummaryDate;

            // Re-apply backend truth
            existing.SubmissionId = storeInRecord.SubmissionId;
            existing.RevisionNo = storeInRecord.RevisionNo;
            existing.StyleNo = storeInRecord.StyleNo;
            existing.Customer = storeInRecord.CustomerName;
            existing.ScheduleNo = storeInRecord.ScheduleNo;
            existing.BodyColour = storeInRecord.BodyColour;
            existing.PrintColour = storeInRecord.PrintColour;
            existing.ReceivedQty = storeInRecord.InQty;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("reports/{id}")]
        public async Task<IActionResult> DeleteCPIReport(string id)
        {
            var report = await _context.CpiReports.FindAsync(id);
            if (report == null) return NotFound();

            _context.CpiReports.Remove(report);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}