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
    public class DeliveryTrackerController : ControllerBase
    {
        private readonly AppDbContext _context;

        private static readonly string[] AllowedStatuses =
        {
            "Pending",
            "In Transit",
            "Delivered",
            "Returned",
            "Delayed"
        };

        public DeliveryTrackerController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // ELIGIBLE GATEPASS ITEMS FOR DELIVERY TRACKING
        // ==========================================

        [HttpGet("eligible-tracking")]
        public async Task<ActionResult<IEnumerable<EligibleDeliveryTrackerDto>>> GetEligibleTrackingItems()
        {
            var trackerReports = await _context.DeliveryTrackers.ToListAsync();
            var adviceNotes = await _context.AdviceNotes
                .OrderByDescending(a => a.DeliveryDate)
                .ToListAsync();

            var eligible = adviceNotes
                .Select(note =>
                {
                    var totalTrackedQty = trackerReports
                        .Where(r => r.AdviceNoteId == note.Id)
                        .Sum(r => r.DeliveryQty);

                    var remainingTrackableQty = Math.Max(0, note.DispatchQty - totalTrackedQty);

                    return new EligibleDeliveryTrackerDto
                    {
                        AdviceNoteId = note.Id,
                        ProductionRecordId = note.ProductionRecordId,
                        StoreInRecordId = note.StoreInRecordId,
                        SubmissionId = note.SubmissionId,
                        RevisionNo = note.RevisionNo,
                        AdNo = note.AdNo,
                        StyleNo = note.StyleNo,
                        CustomerName = note.CustomerName,
                        ScheduleNo = note.ScheduleNo,
                        CutNo = note.CutNo,
                        Component = note.Component,
                        DispatchQty = note.DispatchQty,
                        RemainingTrackableQty = remainingTrackableQty,
                        DeliveryDate = note.DeliveryDate
                    };
                })
                .Where(x => x.RemainingTrackableQty > 0)
                .ToList();

            return Ok(eligible);
        }

        // ==========================================
        // DELIVERY TRACKER REPORTS
        // ==========================================

        [HttpGet("reports")]
        public async Task<ActionResult<IEnumerable<DeliveryTrackerReport>>> GetReports()
        {
            return await _context.DeliveryTrackers
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("reports")]
        public async Task<ActionResult<DeliveryTrackerReport>> CreateReport(DeliveryTrackerReport report)
        {
            if (string.IsNullOrWhiteSpace(report.AdviceNoteId))
                return BadRequest("AdviceNoteId is required.");

            if (string.IsNullOrWhiteSpace(report.CreatedAt))
                return BadRequest("CreatedAt is required.");

            if (report.DeliveryQty <= 0)
                return BadRequest("DeliveryQty must be greater than zero.");

            if (!AllowedStatuses.Contains(report.DeliveryStatus))
                return BadRequest("Invalid DeliveryStatus.");

            var adviceNote = await _context.AdviceNotes
                .FirstOrDefaultAsync(a => a.Id == report.AdviceNoteId);

            if (adviceNote == null)
                return BadRequest("Linked Advice Note not found.");

            var alreadyTrackedQty = await _context.DeliveryTrackers
                .Where(r => r.AdviceNoteId == report.AdviceNoteId)
                .SumAsync(r => r.DeliveryQty);

            var remainingTrackableQty = Math.Max(0, adviceNote.DispatchQty - alreadyTrackedQty);

            if (report.DeliveryQty > remainingTrackableQty)
                return BadRequest($"DeliveryQty exceeds remaining trackable qty ({remainingTrackableQty}).");

            if (string.IsNullOrWhiteSpace(report.Id))
            {
                report.Id = Guid.NewGuid().ToString();
            }

            // Backend source of truth
            report.ProductionRecordId = adviceNote.ProductionRecordId;
            report.StoreInRecordId = adviceNote.StoreInRecordId;
            report.SubmissionId = adviceNote.SubmissionId;
            report.RevisionNo = adviceNote.RevisionNo;
            report.StyleNo = adviceNote.StyleNo;
            report.CustomerName = adviceNote.CustomerName;
            report.AdNo = adviceNote.AdNo;
            report.OrderQty = adviceNote.DispatchQty;
            report.BalanceQty = Math.Max(0, remainingTrackableQty - report.DeliveryQty);

            foreach (var row in report.Rows)
            {
                row.AdviceNoteId = adviceNote.Id;
                row.Style = adviceNote.StyleNo;
                row.Ad = adviceNote.AdNo;
                row.CutNo = adviceNote.CutNo;
                row.Schedule = adviceNote.ScheduleNo;
                row.FpoQty = adviceNote.DispatchQty;
            }

            _context.DeliveryTrackers.Add(report);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReports), new { id = report.Id }, report);
        }

        [HttpPut("reports/{id}")]
        public async Task<IActionResult> UpdateReport(string id, DeliveryTrackerReport report)
        {
            if (id != report.Id)
                return BadRequest("ID mismatch.");

            if (report.DeliveryQty <= 0)
                return BadRequest("DeliveryQty must be greater than zero.");

            if (!AllowedStatuses.Contains(report.DeliveryStatus))
                return BadRequest("Invalid DeliveryStatus.");

            var existing = await _context.DeliveryTrackers
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existing == null)
                return NotFound();

            var adviceNote = await _context.AdviceNotes
                .FirstOrDefaultAsync(a => a.Id == existing.AdviceNoteId);

            if (adviceNote == null)
                return BadRequest("Linked Advice Note not found.");

            var alreadyTrackedQtyExcludingCurrent = await _context.DeliveryTrackers
                .Where(r => r.AdviceNoteId == existing.AdviceNoteId && r.Id != id)
                .SumAsync(r => r.DeliveryQty);

            var remainingTrackableQty = Math.Max(0, adviceNote.DispatchQty - alreadyTrackedQtyExcludingCurrent);

            if (report.DeliveryQty > remainingTrackableQty)
                return BadRequest($"DeliveryQty exceeds remaining trackable qty ({remainingTrackableQty}).");

            existing.FpoNo = report.FpoNo;
            existing.DeliveryQty = report.DeliveryQty;
            existing.DeliveryStatus = report.DeliveryStatus;
            existing.CreatedAt = report.CreatedAt;
            existing.Rows = report.Rows;

            existing.ProductionRecordId = adviceNote.ProductionRecordId;
            existing.StoreInRecordId = adviceNote.StoreInRecordId;
            existing.SubmissionId = adviceNote.SubmissionId;
            existing.RevisionNo = adviceNote.RevisionNo;
            existing.StyleNo = adviceNote.StyleNo;
            existing.CustomerName = adviceNote.CustomerName;
            existing.AdNo = adviceNote.AdNo;
            existing.OrderQty = adviceNote.DispatchQty;
            existing.BalanceQty = Math.Max(0, remainingTrackableQty - report.DeliveryQty);

            foreach (var row in existing.Rows)
            {
                row.AdviceNoteId = adviceNote.Id;
                row.Style = adviceNote.StyleNo;
                row.Ad = adviceNote.AdNo;
                row.CutNo = adviceNote.CutNo;
                row.Schedule = adviceNote.ScheduleNo;
                row.FpoQty = adviceNote.DispatchQty;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("reports/{id}")]
        public async Task<IActionResult> DeleteReport(string id)
        {
            var report = await _context.DeliveryTrackers.FindAsync(id);
            if (report == null) return NotFound();

            _context.DeliveryTrackers.Remove(report);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}