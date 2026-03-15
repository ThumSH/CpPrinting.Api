using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.DTOs;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Gatepass,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class GatepassController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GatepassController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // ELIGIBLE ITEMS FOR GATEPASS
        // ==========================================

        [HttpGet("eligible-dispatch")]
        public async Task<ActionResult<IEnumerable<EligibleGatepassDto>>> GetEligibleDispatchItems()
        {
            var notes = await _context.AdviceNotes.ToListAsync();
            var productionRecords = await _context.StoreProductionRecords
                .OrderByDescending(p => p.IssueDate)
                .ToListAsync();

            var eligible = productionRecords
                .Select(p =>
                {
                    var totalAlreadyDispatched = notes
                        .Where(n => n.ProductionRecordId == p.Id)
                        .Sum(n => n.DispatchQty);

                    var remainingDispatchQty = Math.Max(0, p.IssueQty - totalAlreadyDispatched);

                    return new EligibleGatepassDto
                    {
                        ProductionRecordId = p.Id,
                        StoreInRecordId = p.StoreInRecordId,
                        SubmissionId = p.SubmissionId,
                        RevisionNo = p.RevisionNo,
                        StyleNo = p.StyleNo,
                        CustomerName = p.CustomerName,
                        Components = p.Components,
                        CutNo = p.CutNo,
                        IssueDate = p.IssueDate,
                        LineNo = p.LineNo,
                        IssueQty = p.IssueQty,
                        RemainingDispatchQty = remainingDispatchQty
                    };
                })
                .Where(x => x.RemainingDispatchQty > 0)
                .ToList();

            return Ok(eligible);
        }

        // ==========================================
        // ADVICE NOTES
        // ==========================================

        [HttpGet("advicenotes")]
        public async Task<ActionResult<IEnumerable<AdviceNoteRecord>>> GetAdviceNotes()
        {
            return await _context.AdviceNotes
                .OrderByDescending(n => n.DeliveryDate)
                .ThenByDescending(n => n.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("advicenotes")]
        public async Task<ActionResult<AdviceNoteRecord>> CreateAdviceNote(AdviceNoteRecord note)
        {
            if (string.IsNullOrWhiteSpace(note.ProductionRecordId))
                return BadRequest("ProductionRecordId is required.");

            if (string.IsNullOrWhiteSpace(note.AdNo))
                return BadRequest("AdNo is required.");

            if (string.IsNullOrWhiteSpace(note.DeliveryDate))
                return BadRequest("DeliveryDate is required.");

            if (note.DispatchQty <= 0)
                return BadRequest("DispatchQty must be greater than zero.");

            var productionRecord = await _context.StoreProductionRecords
                .FirstOrDefaultAsync(p => p.Id == note.ProductionRecordId);

            if (productionRecord == null)
                return BadRequest("Linked Production record not found.");

            var totalAlreadyDispatched = await _context.AdviceNotes
                .Where(n => n.ProductionRecordId == note.ProductionRecordId)
                .SumAsync(n => n.DispatchQty);

            var remainingDispatchQty = Math.Max(0, productionRecord.IssueQty - totalAlreadyDispatched);

            if (note.DispatchQty > remainingDispatchQty)
                return BadRequest($"DispatchQty exceeds remaining dispatchable qty ({remainingDispatchQty}).");

            if (string.IsNullOrWhiteSpace(note.Id))
            {
                note.Id = Guid.NewGuid().ToString();
            }

            // Backend source of truth
            note.StoreInRecordId = productionRecord.StoreInRecordId;
            note.SubmissionId = productionRecord.SubmissionId;
            note.RevisionNo = productionRecord.RevisionNo;
            note.StyleNo = productionRecord.StyleNo;
            note.CustomerName = productionRecord.CustomerName;
            note.CutNo = productionRecord.CutNo;
            note.Component = productionRecord.Components;
            note.BalanceQty = Math.Max(0, remainingDispatchQty - note.DispatchQty);

            // Normalize row linkage
            foreach (var row in note.Rows.Values)
            {
                row.ProductionRecordId = productionRecord.Id;
            }

            _context.AdviceNotes.Add(note);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAdviceNotes), new { id = note.Id }, note);
        }

        [HttpPut("advicenotes/{id}")]
        public async Task<IActionResult> UpdateAdviceNote(string id, AdviceNoteRecord note)
        {
            if (id != note.Id)
                return BadRequest("ID mismatch.");

            if (note.DispatchQty <= 0)
                return BadRequest("DispatchQty must be greater than zero.");

            var existing = await _context.AdviceNotes
                .FirstOrDefaultAsync(n => n.Id == id);

            if (existing == null)
                return NotFound();

            var productionRecord = await _context.StoreProductionRecords
                .FirstOrDefaultAsync(p => p.Id == existing.ProductionRecordId);

            if (productionRecord == null)
                return BadRequest("Linked Production record not found.");

            var totalDispatchedExcludingCurrent = await _context.AdviceNotes
                .Where(n => n.ProductionRecordId == existing.ProductionRecordId && n.Id != id)
                .SumAsync(n => n.DispatchQty);

            var remainingDispatchQty = Math.Max(0, productionRecord.IssueQty - totalDispatchedExcludingCurrent);

            if (note.DispatchQty > remainingDispatchQty)
                return BadRequest($"DispatchQty exceeds remaining dispatchable qty ({remainingDispatchQty}).");

            existing.AdNo = note.AdNo;
            existing.DeliveryDate = note.DeliveryDate;
            existing.Attn = note.Attn;
            existing.Address = note.Address;
            existing.ScheduleNo = note.ScheduleNo;
            existing.DispatchQty = note.DispatchQty;
            existing.Rows = note.Rows;
            existing.ReceivedByName = note.ReceivedByName;
            existing.PrepByName = note.PrepByName;
            existing.AuthByName = note.AuthByName;

            existing.StoreInRecordId = productionRecord.StoreInRecordId;
            existing.SubmissionId = productionRecord.SubmissionId;
            existing.RevisionNo = productionRecord.RevisionNo;
            existing.StyleNo = productionRecord.StyleNo;
            existing.CustomerName = productionRecord.CustomerName;
            existing.CutNo = productionRecord.CutNo;
            existing.Component = productionRecord.Components;
            existing.BalanceQty = Math.Max(0, remainingDispatchQty - note.DispatchQty);

            foreach (var row in existing.Rows.Values)
            {
                row.ProductionRecordId = productionRecord.Id;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("advicenotes/{id}")]
        public async Task<IActionResult> DeleteAdviceNote(string id)
        {
            var note = await _context.AdviceNotes.FindAsync(id);
            if (note == null) return NotFound();

            _context.AdviceNotes.Remove(note);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}