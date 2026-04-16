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

        public DeliveryTrackerController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Auto-generated delivery tracker report.
        /// Groups by Style + Schedule (Store-In record).
        /// Each row = one Advice Note dispatched against production records for that store-in.
        /// </summary>
        [HttpGet("report")]
        public async Task<ActionResult<IEnumerable<DeliveryTrackerSummaryDto>>> GetDeliveryTrackerReport(
            [FromQuery] string? styleNo = null,
            [FromQuery] string? scheduleNo = null,
            [FromQuery] int? limit = null)
        {
            // Build filtered store-in query based on params
            var siQuery = _context.StoreInRecords
                .Include(s => s.Cuts)
                    .ThenInclude(c => c.Bundles)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(styleNo))
                siQuery = siQuery.Where(s => s.StyleNo == styleNo);
            if (!string.IsNullOrWhiteSpace(scheduleNo))
                siQuery = siQuery.Where(s => s.ScheduleNo == scheduleNo);

            var storeInRecords = await siQuery.ToListAsync();

            // Load all advice notes
            var adviceNotes = await _context.AdviceNotes
                .OrderBy(a => a.DeliveryDate)
                .ToListAsync();

            // Load approvals for bulk qty
            var approvals = await _context.Approvals.ToListAsync();

            // Load all production records to link advice notes to store-in
            var productionRecords = await _context.StoreProductionRecords.ToListAsync();

            // Build a lookup: productionRecordId -> storeInRecordId
            var prodToStoreIn = productionRecords.ToDictionary(p => p.Id, p => p.StoreInRecordId);

            // Group advice notes by store-in record
            var notesByStoreIn = new Dictionary<string, List<AdviceNoteRecord>>();

            foreach (var note in adviceNotes)
            {
                // The note may have multiple production record IDs (comma-separated)
                // or a direct storeInRecordId
                var storeInId = note.StoreInRecordId;

                if (string.IsNullOrEmpty(storeInId) && !string.IsNullOrEmpty(note.ProductionRecordId))
                {
                    // Try to resolve from first production record ID
                    var firstProdId = note.ProductionRecordId.Split(',').FirstOrDefault()?.Trim();
                    if (firstProdId != null && prodToStoreIn.TryGetValue(firstProdId, out var resolved))
                        storeInId = resolved;
                }

                if (string.IsNullOrEmpty(storeInId)) continue;

                if (!notesByStoreIn.ContainsKey(storeInId))
                    notesByStoreIn[storeInId] = new List<AdviceNoteRecord>();

                notesByStoreIn[storeInId].Add(note);
            }

            // Build summaries — one per store-in record that has advice notes
            var summaries = new List<DeliveryTrackerSummaryDto>();

            foreach (var storeIn in storeInRecords)
            {
                if (!notesByStoreIn.ContainsKey(storeIn.Id)) continue;

                var notes = notesByStoreIn[storeIn.Id];
                if (!notes.Any()) continue;

                // Get bulk qty from approval
                var approval = approvals.FirstOrDefault(a => a.SubmissionId == storeIn.SubmissionId);
                var bulkQty = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;

                // Collect all sizes from bundles
                var allSizes = storeIn.Cuts
                    .SelectMany(c => c.Bundles)
                    .Select(b => b.Size)
                    .Distinct()
                    .OrderBy(s => SizeOrder(s))
                    .ToList();

                // Build size lookup: cutNo -> size -> total bundle qty
                var cutSizeQty = new Dictionary<string, Dictionary<string, int>>();
                foreach (var cut in storeIn.Cuts)
                {
                    var sizeQty = new Dictionary<string, int>();
                    foreach (var bundle in cut.Bundles)
                    {
                        if (!sizeQty.ContainsKey(bundle.Size))
                            sizeQty[bundle.Size] = 0;
                        sizeQty[bundle.Size] += bundle.BundleQty;
                    }
                    cutSizeQty[cut.CutNo] = sizeQty;
                }

                var rows = new List<DeliveryTrackerRowDto>();

                foreach (var note in notes)
                {
                    var noteRows = note.Rows?.Values?.ToList() ?? new List<AdviceNoteRow>();

                    // Group by cutForm to get per-cut data
                    var cutGroups = noteRows.GroupBy(r => r.CutForm).ToList();

                    foreach (var cutGroup in cutGroups)
                    {
                        var cutNo = cutGroup.Key;
                        var bundlesInCut = cutGroup.ToList();
                        var fpoQty = bundlesInCut.Sum(b => b.TotalPcs);
                        var allowedPd = (int)Math.Ceiling(fpoQty * 0.1);

                        // Per-size breakdown from the bundles in this cut
                        var sizeBreakdown = allSizes.Select(size =>
                        {
                            var bundlesForSize = bundlesInCut.Where(b => b.Size == size).ToList();
                            return new DeliveryTrackerSizeData
                            {
                                Size = size,
                                Qty = bundlesForSize.Sum(b => b.TotalPcs),
                                Pd = bundlesForSize.Sum(b => b.Pd),
                                Fd = bundlesForSize.Sum(b => b.Fd)
                            };
                        }).ToList();

                        var sizePdTotal = sizeBreakdown.Sum(s => s.Pd);
                        var fdTotal = sizeBreakdown.Sum(s => s.Fd);

                        rows.Add(new DeliveryTrackerRowDto
                        {
                            InDate = storeIn.CutInDate ?? string.Empty,
                            DeliveryDate = note.DeliveryDate,
                            StyleNo = storeIn.StyleNo ?? string.Empty,
                            Colour = storeIn.BodyColour ?? string.Empty,
                            InAd = note.AdNo,
                            Ad = note.AdNo,
                            ScheduleNo = storeIn.ScheduleNo,
                            FpoQty = fpoQty,
                            AllowedPd = allowedPd,
                            CutNo = cutNo,
                            SizeBreakdown = sizeBreakdown,
                            TotalQty = sizeBreakdown.Sum(s => s.Qty),
                            SizePdTotal = sizePdTotal,
                            FdTotal = fdTotal,
                            Exceeded = Math.Max(0, sizePdTotal - allowedPd)
                        });
                    }
                }

                // Size totals across all rows
                var sizeTotals = allSizes.Select(size => new DeliveryTrackerSizeData
                {
                    Size = size,
                    Qty = rows.Sum(r => r.SizeBreakdown.Where(s => s.Size == size).Sum(s => s.Qty)),
                    Pd = rows.Sum(r => r.SizeBreakdown.Where(s => s.Size == size).Sum(s => s.Pd)),
                    Fd = rows.Sum(r => r.SizeBreakdown.Where(s => s.Size == size).Sum(s => s.Fd))
                }).ToList();

                var totalDelivered = notes.Sum(n => n.DispatchQty);
                var grandPd = rows.Sum(r => r.SizePdTotal);
                var grandFd = rows.Sum(r => r.FdTotal);
                var grandQty = rows.Sum(r => r.TotalQty);

                summaries.Add(new DeliveryTrackerSummaryDto
                {
                    StoreInRecordId = storeIn.Id,
                    StyleNo = storeIn.StyleNo ?? string.Empty,
                    FpoNo = storeIn.ScheduleNo,
                    CustomerName = storeIn.CustomerName ?? string.Empty,
                    OrderQty = bulkQty,
                    ReceivedQty = storeIn.InQty,
                    DeliveredQty = totalDelivered,
                    BalanceToRec = Math.Max(0, bulkQty - storeIn.InQty),
                    PdTotal = grandPd,
                    PdPercentage = grandQty > 0
                        ? ((double)grandPd / grandQty * 100).ToString("F2")
                        : "0.00",
                    AllSizes = allSizes,
                    Rows = rows,
                    SizeTotals = sizeTotals,
                    GrandTotalQty = grandQty,
                    GrandPdTotal = grandPd,
                    GrandFdTotal = grandFd
                });
            }

            // Sort by most recent delivery date across the rows within each summary.
            // DeliveryDate lives on the Rows, not on the summary itself.
            summaries = summaries
                .OrderByDescending(s => s.Rows != null && s.Rows.Count > 0
                    ? s.Rows.Max(r => r.DeliveryDate ?? "")
                    : "")
                .ToList();

            if (limit.HasValue && limit.Value > 0)
                summaries = summaries.Take(limit.Value).ToList();

            return Ok(summaries);
        }

        // ==========================================
        // SAVED TRACKER REPORTS
        // ==========================================

        /// <summary>
        /// Get all saved delivery tracker reports.
        /// </summary>
        /// <summary>
        /// Lightweight endpoint — returns distinct styleNo + scheduleNo combinations.
        /// Used by the dropdowns on the tracker page without loading the full report.
        /// </summary>
        [HttpGet("filters")]
        public async Task<ActionResult> GetTrackerFilters()
        {
            // Only include store-ins that have at least one advice note (otherwise no tracker row would exist)
            var prodMap = await _context.StoreProductionRecords
                .Select(p => new { p.Id, p.StoreInRecordId })
                .ToListAsync();
            var prodIdToStoreIn = prodMap.ToDictionary(p => p.Id, p => p.StoreInRecordId);

            var adviceNotes = await _context.AdviceNotes
                .Select(a => new { a.ProductionRecordId, a.StyleNo })
                .ToListAsync();

            var storeInIdsWithDispatch = adviceNotes
                .Where(a => prodIdToStoreIn.ContainsKey(a.ProductionRecordId))
                .Select(a => prodIdToStoreIn[a.ProductionRecordId])
                .ToHashSet();

            var combos = await _context.StoreInRecords
                .Where(s => storeInIdsWithDispatch.Contains(s.Id))
                .Select(s => new { StyleNo = s.StyleNo ?? "", ScheduleNo = s.ScheduleNo ?? "" })
                .Distinct()
                .ToListAsync();

            return Ok(combos);
        }

        [HttpGet("saved")]
        public async Task<ActionResult<IEnumerable<DeliveryTrackerReport>>> GetSavedReports()
        {
            return await _context.DeliveryTrackers
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Save a delivery tracker report snapshot to the database.
        /// </summary>
        [HttpPost("save")]
        public async Task<ActionResult<DeliveryTrackerReport>> SaveTrackerReport(DeliveryTrackerReport report)
        {
            if (string.IsNullOrWhiteSpace(report.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(report.StyleNo))
                return BadRequest("StyleNo is required.");

            // Check if a saved report already exists for this store-in
            var existing = await _context.DeliveryTrackers
                .FirstOrDefaultAsync(r => r.StoreInRecordId == report.StoreInRecordId
                                         && r.FpoNo == report.FpoNo);

            if (existing != null)
            {
                // Update existing report
                existing.OrderQty = report.OrderQty;
                existing.DeliveryQty = report.DeliveryQty;
                existing.BalanceQty = report.BalanceQty;
                existing.DeliveryStatus = report.DeliveryStatus;
                existing.Rows = report.Rows;
                existing.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                await _context.SaveChangesAsync();
                return Ok(existing);
            }
            else
            {
                // Create new
                if (string.IsNullOrWhiteSpace(report.Id))
                    report.Id = Guid.NewGuid().ToString();

                report.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                _context.DeliveryTrackers.Add(report);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetSavedReports), new { id = report.Id }, report);
            }
        }

        /// <summary>
        /// Delete a saved delivery tracker report.
        /// </summary>
        [HttpDelete("saved/{id}")]
        public async Task<IActionResult> DeleteSavedReport(string id)
        {
            var report = await _context.DeliveryTrackers.FindAsync(id);
            if (report == null) return NotFound();

            _context.DeliveryTrackers.Remove(report);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Helper: sort sizes in standard order
        private static int SizeOrder(string size)
        {
            return size.ToUpper() switch
            {
                "XXS" => 0, "XS" => 1, "S" => 2, "M" => 3, "L" => 4,
                "XL" => 5, "2XL" => 6, "3XL" => 7, "4XL" => 8,
                _ => 99
            };
        }
    }
}