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
        // HELPER: Preserve Store-In bundle row order
        // ==========================================
        private static List<BundleRecord> OrderBundlesBySavedStoreInOrder(IEnumerable<BundleRecord>? bundles)
        {
            var list = (bundles ?? Enumerable.Empty<BundleRecord>()).ToList();

            // New Store-In records have BundleOrder saved. Old records may have 0,
            // so only order when BundleOrder is actually present. Never sort by
            // BundleNo, because manual sequences like b-12, b-2, b-5, b-10
            // must stay exactly as entered.
            return list.Any(b => b.BundleOrder > 0)
                ? list.OrderBy(b => b.BundleOrder > 0 ? b.BundleOrder : int.MaxValue).ToList()
                : list;
        }

        // ==========================================
        // ELIGIBLE ITEMS FOR GATEPASS
        // Source: Production records (which come from QC-passed Store-In)
        // Enriched with Store-In data for full context
        // ==========================================

        [HttpGet("eligible-dispatch")]
        public async Task<ActionResult<IEnumerable<EligibleGatepassDto>>> GetEligibleDispatchItems()
        {
            // ── Flow: Store-In (cuts+bundles) → CPI passed → Gatepass ──────────
            // Production records are a separate concern and do NOT gate the gatepass.
            // Eligible = StoreIn records that have a PASSED CPI report and still
            // have remaining qty to dispatch (totalCutQty - alreadyDispatched).

            var notes = await _context.AdviceNotes.ToListAsync();

            // Only StoreIn records with a CPI-Passed report are eligible
            var passedCpiStoreInIds = await _context.CpiReports
                .Where(r => r.InspectionStatus == "Passed")
                .Select(r => r.StoreInRecordId)
                .ToListAsync();

            if (!passedCpiStoreInIds.Any())
                return Ok(new List<EligibleGatepassDto>());

            var storeInRecords = await _context.StoreInRecords
                .Include(s => s.Cuts)
                    .ThenInclude(c => c.Bundles)
                .Where(s => passedCpiStoreInIds.Contains(s.Id))
                .ToListAsync();

            // Load CPI reports to get Part per cut (from QC inspection)
            var cpiReports = await _context.CpiReports
                .Where(r => passedCpiStoreInIds.Contains(r.StoreInRecordId))
                .ToListAsync();
            var cpiByStoreIn = cpiReports.ToDictionary(r => r.StoreInRecordId);

            var eligible = storeInRecords.Select(storeIn =>
            {
                // Total qty = sum of all cut qtys in this StoreIn
                var totalCutQty = storeIn.Cuts.Sum(c => c.CutQty);

                // Already dispatched against this StoreIn
                var totalAlreadyDispatched = notes
                    .Where(n => n.StoreInRecordId == storeIn.Id)
                    .Sum(n => n.DispatchQty);

                var remainingDispatchQty = Math.Max(0, totalCutQty - totalAlreadyDispatched);

                return new EligibleGatepassDto
                {
                    ProductionRecordId = string.Empty, // not used in this flow
                    StoreInRecordId    = storeIn.Id,
                    SubmissionId       = storeIn.SubmissionId,
                    RevisionNo         = storeIn.RevisionNo,
                    StyleNo            = storeIn.StyleNo      ?? string.Empty,
                    CustomerName       = storeIn.CustomerName ?? string.Empty,
                    Components         = storeIn.Components   ?? string.Empty,
                    CutNo              = string.Join(", ", storeIn.Cuts.Select(c => c.CutNo)),
                    IssueDate          = storeIn.CutInDate    ?? string.Empty,
                    LineNo             = string.Empty,
                    IssueQty           = totalCutQty,
                    RemainingDispatchQty = remainingDispatchQty,
                    ScheduleNo         = storeIn.ScheduleNo   ?? string.Empty,
                    JobNo              = storeIn.JobNo         ?? string.Empty,
                    BodyColour         = storeIn.BodyColour    ?? string.Empty,
                    PrintColour        = storeIn.PrintColour   ?? string.Empty,
                    Season             = storeIn.Season        ?? string.Empty,
                    Cuts = storeIn.Cuts.Select(c =>
                    {
                        cpiByStoreIn.TryGetValue(storeIn.Id, out var cpiForStoreIn);
                        var cpiCut = cpiForStoreIn?.CutInspections
                            ?.FirstOrDefault(ci => ci.CutNo == c.CutNo);
                        var cutPart = cpiCut?.Part ?? string.Empty;

                        var orderedBundles = OrderBundlesBySavedStoreInOrder(c.Bundles);

                        return new GatepassCutDto
                        {
                            CutNo  = c.CutNo,
                            CutQty = c.CutQty,
                            Part   = cutPart,
                            Bundles = orderedBundles.Select((b, index) => new GatepassBundleDto
                            {
                                BundleNo    = b.BundleNo,
                                BundleOrder = b.BundleOrder > 0 ? b.BundleOrder : index + 1,
                                BundleQty   = b.BundleQty,
                                Size        = b.Size,
                                NumberRange = b.NumberRange ?? string.Empty
                            }).ToList()
                        };
                    }).ToList()
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

            // ── Validate against StoreIn cuts directly — no production dependency ──
            var storeInWithCuts = await _context.StoreInRecords
                .Include(s => s.Cuts)
                .FirstOrDefaultAsync(s => s.Id == note.StoreInRecordId);

            var totalCutQty = storeInWithCuts?.Cuts.Sum(c => c.CutQty) ?? 0;
            if (totalCutQty <= 0)
                return BadRequest("No cuts found for this Store-In record.");

            // CPI must have passed before dispatch
            var cpiPassed = await _context.CpiReports
                .AnyAsync(r => r.StoreInRecordId == note.StoreInRecordId
                            && r.InspectionStatus == "Passed");
            if (!cpiPassed)
                return BadRequest("CPI inspection must be Passed before dispatching.");

            var totalAlreadyDispatched = await _context.AdviceNotes
                .Where(n => n.StoreInRecordId == note.StoreInRecordId)
                .SumAsync(n => n.DispatchQty);

            var remainingDispatchQty = Math.Max(0, totalCutQty - totalAlreadyDispatched);

            if (note.DispatchQty > remainingDispatchQty)
                return BadRequest($"DispatchQty exceeds remaining dispatchable qty ({remainingDispatchQty}).");

            if (string.IsNullOrWhiteSpace(note.Id))
                note.Id = Guid.NewGuid().ToString();

            // Backend source of truth
            note.ProductionRecordId = string.Empty;
            note.SubmissionId  = storeIn.SubmissionId;
            note.RevisionNo    = storeIn.RevisionNo;
            note.StyleNo       = storeIn.StyleNo      ?? string.Empty;
            note.CustomerName  = storeIn.CustomerName  ?? string.Empty;
            note.CutNo         = string.Join(", ", storeInWithCuts?.Cuts.Select(c => c.CutNo).Distinct() ?? Enumerable.Empty<string>());
            note.Component     = storeIn.Components    ?? string.Empty;
            note.BalanceQty    = Math.Max(0, remainingDispatchQty - note.DispatchQty);
            note.ScheduleNo    = storeIn.ScheduleNo    ?? string.Empty;
            note.JobNo         = storeIn.JobNo         ?? string.Empty;

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

            // Validate against StoreIn cuts directly
            var storeInWithCuts = await _context.StoreInRecords
                .Include(s => s.Cuts)
                .FirstOrDefaultAsync(s => s.Id == existing.StoreInRecordId);

            var totalCutQty = storeInWithCuts?.Cuts.Sum(c => c.CutQty) ?? 0;

            var totalDispatchedExcludingCurrent = await _context.AdviceNotes
                .Where(n => n.StoreInRecordId == existing.StoreInRecordId && n.Id != id)
                .SumAsync(n => n.DispatchQty);

            var remainingDispatchQty = Math.Max(0, totalCutQty - totalDispatchedExcludingCurrent);

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
            existing.ScheduleNo = storeIn.ScheduleNo ?? string.Empty;
            existing.JobNo = storeIn.JobNo ?? string.Empty;
            existing.Component = storeIn.Components ?? string.Empty;
            existing.CutNo = string.Join(", ", storeInWithCuts?.Cuts.Select(c => c.CutNo).Distinct() ?? Enumerable.Empty<string>());

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