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
        // Source: Production records (which come from QC-passed Store-In)
        // Enriched with Store-In data for full context
        // ==========================================

        [HttpGet("eligible-dispatch")]
        public async Task<ActionResult<IEnumerable<EligibleGatepassDto>>> GetEligibleDispatchItems()
        {
            var notes = await _context.AdviceNotes.ToListAsync();

            var productionRecords = await _context.StoreProductionRecords
                .OrderByDescending(p => p.IssueDate)
                .ToListAsync();

            // Load store-in records WITH cuts and bundles
            var storeInIds = productionRecords
                .Select(p => p.StoreInRecordId)
                .Distinct()
                .ToList();

            var storeInRecords = await _context.StoreInRecords
                .Include(s => s.Cuts)
                    .ThenInclude(c => c.Bundles)
                .Where(s => storeInIds.Contains(s.Id))
                .ToListAsync();

            // Group by store-in record — one eligible item per style/schedule
            var eligible = storeInRecords.Select(storeIn =>
            {
                var productionsForThisStoreIn = productionRecords
                    .Where(p => p.StoreInRecordId == storeIn.Id)
                    .ToList();

                if (!productionsForThisStoreIn.Any()) return null;

                var totalIssued = productionsForThisStoreIn.Sum(p => p.IssueQty);
                var totalAlreadyDispatched = notes
                    .Where(n => productionsForThisStoreIn.Select(p => p.Id).Contains(n.ProductionRecordId))
                    .Sum(n => n.DispatchQty);

                var remainingDispatchQty = Math.Max(0, totalIssued - totalAlreadyDispatched);

                var firstProd = productionsForThisStoreIn.First();

                // Collect all production record IDs for this store-in
                var productionRecordIds = productionsForThisStoreIn.Select(p => p.Id).ToList();

                return new EligibleGatepassDto
                {
                    ProductionRecordId = string.Join(",", productionRecordIds),
                    StoreInRecordId = storeIn.Id,
                    SubmissionId = storeIn.SubmissionId,
                    RevisionNo = storeIn.RevisionNo,
                    StyleNo = storeIn.StyleNo ?? string.Empty,
                    CustomerName = storeIn.CustomerName ?? string.Empty,
                    Components = storeIn.Components ?? string.Empty,
                    CutNo = string.Join(", ", productionsForThisStoreIn.Select(p => p.CutNo).Distinct()),
                    IssueDate = firstProd.IssueDate ?? string.Empty,
                    LineNo = string.Join(", ", productionsForThisStoreIn.Select(p => p.LineNo).Distinct()),
                    IssueQty = totalIssued,
                    RemainingDispatchQty = remainingDispatchQty,
                    ScheduleNo = storeIn.ScheduleNo,
                    BodyColour = storeIn.BodyColour ?? string.Empty,
                    PrintColour = storeIn.PrintColour ?? string.Empty,
                    Season = storeIn.Season ?? string.Empty,
                    Cuts = storeIn.Cuts?.Select(c => new GatepassCutDto
                    {
                        CutNo = c.CutNo,
                        CutQty = c.CutQty,
                        Bundles = c.Bundles?.Select(b => new GatepassBundleDto
                        {
                            BundleNo = b.BundleNo,
                            BundleQty = b.BundleQty,
                            Size = b.Size,
                            NumberRange = b.NumberRange ?? string.Empty
                        }).ToList() ?? new()
                    }).ToList() ?? new()
                };
            })
            .Where(x => x != null && x.RemainingDispatchQty > 0)
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
            if (string.IsNullOrWhiteSpace(note.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(note.AdNo))
                return BadRequest("AdNo is required.");

            if (string.IsNullOrWhiteSpace(note.DeliveryDate))
                return BadRequest("DeliveryDate is required.");

            if (note.DispatchQty <= 0)
                return BadRequest("DispatchQty must be greater than zero.");

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(s => s.Id == note.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            // Get all production records for this store-in
            var productionRecords = await _context.StoreProductionRecords
                .Where(p => p.StoreInRecordId == note.StoreInRecordId)
                .ToListAsync();

            if (!productionRecords.Any())
                return BadRequest("No production records found for this Store-In record.");

            var totalIssued = productionRecords.Sum(p => p.IssueQty);
            var productionRecordIds = productionRecords.Select(p => p.Id).ToList();

            var totalAlreadyDispatched = await _context.AdviceNotes
                .Where(n => productionRecordIds.Contains(n.ProductionRecordId) ||
                            n.StoreInRecordId == note.StoreInRecordId)
                .SumAsync(n => n.DispatchQty);

            var remainingDispatchQty = Math.Max(0, totalIssued - totalAlreadyDispatched);

            if (note.DispatchQty > remainingDispatchQty)
                return BadRequest($"DispatchQty exceeds remaining dispatchable qty ({remainingDispatchQty}).");

            if (string.IsNullOrWhiteSpace(note.Id))
                note.Id = Guid.NewGuid().ToString();

            // Backend source of truth
            note.ProductionRecordId = string.Join(",", productionRecordIds);
            note.SubmissionId = storeIn.SubmissionId;
            note.RevisionNo = storeIn.RevisionNo;
            note.StyleNo = storeIn.StyleNo ?? string.Empty;
            note.CustomerName = storeIn.CustomerName ?? string.Empty;
            note.CutNo = string.Join(", ", productionRecords.Select(p => p.CutNo).Distinct());
            note.Component = storeIn.Components ?? string.Empty;
            note.BalanceQty = Math.Max(0, remainingDispatchQty - note.DispatchQty);

            if (storeIn != null)
                note.ScheduleNo = storeIn.ScheduleNo;

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

            // Update editable fields
            existing.AdNo = note.AdNo;
            existing.DeliveryDate = note.DeliveryDate;
            existing.Attn = note.Attn;
            existing.Address = note.Address;
            existing.DispatchQty = note.DispatchQty;
            existing.BalanceQty = Math.Max(0, remainingDispatchQty - note.DispatchQty);
            existing.Rows = note.Rows;
            existing.ReceivedByName = note.ReceivedByName;
            existing.PrepByName = note.PrepByName;
            existing.AuthByName = note.AuthByName;

            // Re-apply backend truth
            existing.StyleNo = productionRecord.StyleNo ?? string.Empty;
            existing.CustomerName = productionRecord.CustomerName ?? string.Empty;
            existing.CutNo = productionRecord.CutNo ?? string.Empty;
            existing.Component = productionRecord.Components ?? string.Empty;

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