using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;
using CpPrinting.Api.DTOs;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Gatepass,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class GatepassController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public GatepassController(AppDbContext context, ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
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

            // Load CPI reports so we can pull the Part chosen for each cut.
            // Note: CutInspections is a JSON column — no .Include() needed; it deserializes automatically.
            var cpiReports = await _context.CpiReports
                .Where(r => storeInIds.Contains(r.StoreInRecordId))
                .ToListAsync();
            var cpiByStoreIn = cpiReports.ToDictionary(r => r.StoreInRecordId);

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
                    Cuts = storeIn.Cuts?.Select(c =>
                    {
                        // Look up the Part chosen by QC for this specific cut
                        cpiByStoreIn.TryGetValue(storeIn.Id, out var cpiForStoreIn);
                        var cpiCut = cpiForStoreIn?.CutInspections?.FirstOrDefault(ci => ci.CutNo == c.CutNo);
                        var cutPart = cpiCut?.Part ?? string.Empty;

                        return new GatepassCutDto
                        {
                            CutNo = c.CutNo,
                            CutQty = c.CutQty,
                            Part = cutPart,
                            Bundles = c.Bundles?.Select(b => new GatepassBundleDto
                            {
                                BundleNo = b.BundleNo,
                                BundleQty = b.BundleQty,
                                Size = b.Size,
                                NumberRange = b.NumberRange ?? string.Empty
                            }).ToList() ?? new()
                        };
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

            await _logger.Log(User, HttpContext, "Create", "AdviceNote", note.Id,
                $"Created advice note {note.AdNo} for {note.StyleNo} — {note.DispatchQty} pcs");

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

            // Validate dispatch qty against all production records for this store-in
            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(s => s.Id == existing.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            var productionRecords = await _context.StoreProductionRecords
                .Where(p => p.StoreInRecordId == existing.StoreInRecordId)
                .ToListAsync();

            var totalIssued = productionRecords.Sum(p => p.IssueQty);

            var totalDispatchedExcludingCurrent = await _context.AdviceNotes
                .Where(n => n.StoreInRecordId == existing.StoreInRecordId && n.Id != id)
                .SumAsync(n => n.DispatchQty);

            var remainingDispatchQty = Math.Max(0, totalIssued - totalDispatchedExcludingCurrent);

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
            existing.Remarks = note.Remarks;

            // Re-apply backend truth from store-in
            existing.StyleNo = storeIn.StyleNo ?? string.Empty;
            existing.CustomerName = storeIn.CustomerName ?? string.Empty;
            existing.ScheduleNo = storeIn.ScheduleNo;
            existing.Component = storeIn.Components ?? string.Empty;
            existing.CutNo = string.Join(", ", productionRecords.Select(p => p.CutNo).Distinct());

            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Update", "AdviceNote", id,
                $"Updated advice note {existing.AdNo} — {existing.DispatchQty} pcs");

            return NoContent();
        }

        [HttpDelete("advicenotes/{id}")]
        public async Task<IActionResult> DeleteAdviceNote(string id)
        {
            var note = await _context.AdviceNotes.FindAsync(id);
            if (note == null) return NotFound();

            _context.AdviceNotes.Remove(note);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "AdviceNote", id,
                $"Deleted advice note {note.AdNo} for {note.StyleNo}");

            return NoContent();
        }
    }
}