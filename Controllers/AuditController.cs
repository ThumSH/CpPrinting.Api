using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.DTOs;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Audit,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly string[] AllowedStatuses = { "Pending", "Pass", "Fail" };

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("eligible-audits")]
        public async Task<ActionResult<IEnumerable<EligibleAuditDto>>> GetEligibleAudits()
        {
            var auditRecords = await _context.AuditRecords.ToListAsync();
            var trackerReports = await _context.DeliveryTrackers
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var eligible = trackerReports
                .Select(report =>
                {
                    var alreadyAuditedQty = auditRecords
                        .Where(a => a.DeliveryTrackerReportId == report.Id)
                        .Sum(a => a.AuditQty);

                    var remainingAuditQty = Math.Max(0, report.DeliveryQty - alreadyAuditedQty);

                    return new EligibleAuditDto
                    {
                        DeliveryTrackerReportId = report.Id,
                        AdviceNoteId = report.AdviceNoteId,
                        ProductionRecordId = report.ProductionRecordId,
                        StoreInRecordId = report.StoreInRecordId,
                        SubmissionId = report.SubmissionId,
                        RevisionNo = report.RevisionNo,
                        StyleNo = report.StyleNo,
                        CustomerName = report.CustomerName,
                        ScheduleNo = report.FpoNo,
                        CutNo = report.Rows.FirstOrDefault()?.CutNo ?? string.Empty,
                        AdNo = report.AdNo,
                        DeliveryStatus = report.DeliveryStatus,
                        DeliveryQty = report.DeliveryQty,
                        RemainingAuditQty = remainingAuditQty,
                        CreatedAt = report.CreatedAt
                    };
                })
                .Where(x => x.RemainingAuditQty > 0)
                .ToList();

            return Ok(eligible);
        }

        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<AuditRecord>>> GetAuditRecords()
        {
            return await _context.AuditRecords
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("records")]
        public async Task<ActionResult<AuditRecord>> CreateAuditRecord(AuditRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.DeliveryTrackerReportId))
                return BadRequest("DeliveryTrackerReportId is required.");

            if (string.IsNullOrWhiteSpace(record.Date))
                return BadRequest("Date is required.");

            if (record.AuditQty <= 0)
                return BadRequest("AuditQty must be greater than zero.");

            if (!AllowedStatuses.Contains(record.Status))
                return BadRequest("Invalid Status. Allowed: Pending, Pass, Fail.");

            var trackerReport = await _context.DeliveryTrackers
                .FirstOrDefaultAsync(r => r.Id == record.DeliveryTrackerReportId);

            if (trackerReport == null)
                return BadRequest("Linked delivery tracker report not found.");

            var alreadyAuditedQty = await _context.AuditRecords
                .Where(a => a.DeliveryTrackerReportId == record.DeliveryTrackerReportId)
                .SumAsync(a => a.AuditQty);

            var remainingAuditQty = Math.Max(0, trackerReport.DeliveryQty - alreadyAuditedQty);

            if (record.AuditQty > remainingAuditQty)
                return BadRequest($"AuditQty exceeds remaining auditable qty ({remainingAuditQty}).");

            if (string.IsNullOrWhiteSpace(record.Id))
            {
                record.Id = Guid.NewGuid().ToString();
            }

            // backend source of truth
            record.AdviceNoteId = trackerReport.AdviceNoteId;
            record.ProductionRecordId = trackerReport.ProductionRecordId;
            record.StoreInRecordId = trackerReport.StoreInRecordId;
            record.SubmissionId = trackerReport.SubmissionId;
            record.RevisionNo = trackerReport.RevisionNo;
            record.StyleNo = trackerReport.StyleNo;
            record.CustomerName = trackerReport.CustomerName;
            record.ScheduleNo = trackerReport.FpoNo;
            record.CutNo = trackerReport.Rows.FirstOrDefault()?.CutNo ?? string.Empty;
            record.Colour = trackerReport.Rows.FirstOrDefault()?.Colour ?? string.Empty;
            record.AdNo = trackerReport.AdNo;
            record.DeliveryStatus = trackerReport.DeliveryStatus;
            record.TotalQty = trackerReport.DeliveryQty;

            _context.AuditRecords.Add(record);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAuditRecords), new { id = record.Id }, record);
        }

        [HttpPatch("records/{id}/status")]
        public async Task<IActionResult> UpdateAuditStatus(string id, [FromBody] UpdateStatusDto dto)
        {
            var record = await _context.AuditRecords.FindAsync(id);
            if (record == null) return NotFound();

            if (!AllowedStatuses.Contains(dto.Status))
                return BadRequest("Invalid Status. Allowed: Pending, Pass, Fail.");

            record.Status = dto.Status;
            record.Remarks = dto.Remarks;
            record.AuditorName = dto.AuditorName;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("records/{id}")]
        public async Task<IActionResult> UpdateAuditRecord(string id, AuditRecord record)
        {
            if (id != record.Id)
                return BadRequest("ID mismatch.");

            if (!AllowedStatuses.Contains(record.Status))
                return BadRequest("Invalid Status. Allowed: Pending, Pass, Fail.");

            var existing = await _context.AuditRecords
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existing == null)
                return NotFound();

            var trackerReport = await _context.DeliveryTrackers
                .FirstOrDefaultAsync(r => r.Id == existing.DeliveryTrackerReportId);

            if (trackerReport == null)
                return BadRequest("Linked delivery tracker report not found.");

            var alreadyAuditedQtyExcludingCurrent = await _context.AuditRecords
                .Where(a => a.DeliveryTrackerReportId == existing.DeliveryTrackerReportId && a.Id != id)
                .SumAsync(a => a.AuditQty);

            var remainingAuditQty = Math.Max(0, trackerReport.DeliveryQty - alreadyAuditedQtyExcludingCurrent);

            if (record.AuditQty <= 0)
                return BadRequest("AuditQty must be greater than zero.");

            if (record.AuditQty > remainingAuditQty)
                return BadRequest($"AuditQty exceeds remaining auditable qty ({remainingAuditQty}).");

            existing.Date = record.Date;
            existing.Bundles = record.Bundles;
            existing.Sizes = record.Sizes;
            existing.TotalQty = trackerReport.DeliveryQty;
            existing.AuditQty = record.AuditQty;
            existing.Status = record.Status;
            existing.AuditorName = record.AuditorName;
            existing.Remarks = record.Remarks;

            existing.AdviceNoteId = trackerReport.AdviceNoteId;
            existing.ProductionRecordId = trackerReport.ProductionRecordId;
            existing.StoreInRecordId = trackerReport.StoreInRecordId;
            existing.SubmissionId = trackerReport.SubmissionId;
            existing.RevisionNo = trackerReport.RevisionNo;
            existing.StyleNo = trackerReport.StyleNo;
            existing.CustomerName = trackerReport.CustomerName;
            existing.ScheduleNo = trackerReport.FpoNo;
            existing.CutNo = trackerReport.Rows.FirstOrDefault()?.CutNo ?? string.Empty;
            existing.Colour = trackerReport.Rows.FirstOrDefault()?.Colour ?? string.Empty;
            existing.AdNo = trackerReport.AdNo;
            existing.DeliveryStatus = trackerReport.DeliveryStatus;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("records/{id}")]
        public async Task<IActionResult> DeleteAuditRecord(string id)
        {
            var record = await _context.AuditRecords.FindAsync(id);
            if (record == null) return NotFound();

            _context.AuditRecords.Remove(record);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string AuditorName { get; set; } = string.Empty;
    }
}