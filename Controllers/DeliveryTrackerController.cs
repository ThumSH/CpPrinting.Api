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
        /// </summary>
        [HttpGet("report")]
        public async Task<ActionResult<IEnumerable<DeliveryTrackerSummaryDto>>> GetDeliveryTrackerReport(
            [FromQuery] string? styleNo = null,
            [FromQuery] string? scheduleNo = null,
            [FromQuery] int? limit = null)
        {
            // 1. DB OPTIMIZATION: Use AsNoTracking() for heavy read-only queries
            var siQuery = _context.StoreInRecords
                .AsNoTracking()
                .Include(s => s.Cuts)
                    .ThenInclude(c => c.Bundles)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(styleNo))
                siQuery = siQuery.Where(s => s.StyleNo == styleNo);
            if (!string.IsNullOrWhiteSpace(scheduleNo))
                siQuery = siQuery.Where(s => s.ScheduleNo == scheduleNo);

            var storeInRecords = await siQuery.ToListAsync();

            if (!storeInRecords.Any())
                return Ok(new List<DeliveryTrackerSummaryDto>());

            var storeInIds = storeInRecords.Select(s => s.Id).ToList();
            var submissionIds = storeInRecords.Select(s => s.SubmissionId).ToList();

            // 2. DB OPTIMIZATION: Only fetch Approvals we actually need.
            // Safety check: Avoid SQL 2100 parameter limit crash if list is massive.
            List<ApprovalRecord> approvals;
            if (submissionIds.Count > 1500)
                approvals = await _context.Approvals.AsNoTracking().ToListAsync();
            else
                approvals = await _context.Approvals.AsNoTracking()
                    .Where(a => submissionIds.Contains(a.SubmissionId)).ToListAsync();

            // 3. DB OPTIMIZATION: Only fetch specific Production Records
            List<StoreProductionRecord> productionRecords;
            if (storeInIds.Count > 1500)
                productionRecords = await _context.StoreProductionRecords.AsNoTracking().ToListAsync();
            else
                productionRecords = await _context.StoreProductionRecords.AsNoTracking()
                    .Where(p => storeInIds.Contains(p.StoreInRecordId)).ToListAsync();

            var prodToStoreIn = productionRecords.ToDictionary(p => p.Id, p => p.StoreInRecordId);

            // 4. Load advice notes (AsNoTracking to save memory).
            var adviceNotes = await _context.AdviceNotes
                .AsNoTracking()
                .OrderBy(a => a.DeliveryDate)
                .ToListAsync();

            // Group advice notes by store-in record
            var notesByStoreIn = new Dictionary<string, List<AdviceNoteRecord>>();

            foreach (var note in adviceNotes)
            {
                var storeInId = note.StoreInRecordId;

                if (string.IsNullOrEmpty(storeInId) && !string.IsNullOrEmpty(note.ProductionRecordId))
                {
                    var firstProdId = note.ProductionRecordId.Split(',').FirstOrDefault()?.Trim();
                    if (firstProdId != null && prodToStoreIn.TryGetValue(firstProdId, out var resolved))
                        storeInId = resolved;
                }

                if (string.IsNullOrEmpty(storeInId)) continue;

                if (!notesByStoreIn.ContainsKey(storeInId))
                    notesByStoreIn[storeInId] = new List<AdviceNoteRecord>();

                notesByStoreIn[storeInId].Add(note);
            }

            var summaries = new List<DeliveryTrackerSummaryDto>();

            foreach (var storeIn in storeInRecords)
            {
                if (!notesByStoreIn.ContainsKey(storeIn.Id)) continue;

                var notes = notesByStoreIn[storeIn.Id];
                if (!notes.Any()) continue;

                var approval = approvals.FirstOrDefault(a => a.SubmissionId == storeIn.SubmissionId);
                var bulkQty = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;

                var allSizes = storeIn.Cuts
                    .SelectMany(c => c.Bundles)
                    .Select(b => b.Size)
                    .Distinct()
                    .OrderBy(s => SizeOrder(s))
                    .ToList();

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
                    var cutGroups = noteRows.GroupBy(r => r.CutForm).ToList();

                    foreach (var cutGroup in cutGroups)
                    {
                        var cutNo = cutGroup.Key;
                        var bundlesInCut = cutGroup.ToList();
                        var fpoQty = bundlesInCut.Sum(b => b.TotalPcs);
                        var allowedPd = (int)Math.Ceiling(fpoQty * 0.1);

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
                            InAd = storeIn.InAdNo ?? string.Empty,
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

            summaries = summaries
                .OrderByDescending(s => s.Rows != null && s.Rows.Count > 0
                    ? s.Rows.Max(r => r.DeliveryDate ?? "")
                    : "")
                .ToList();

            if (limit.HasValue && limit.Value > 0)
                summaries = summaries.Take(limit.Value).ToList();

            return Ok(summaries);
        }

        [HttpGet("filters")]
        public async Task<ActionResult> GetTrackerFilters()
        {
            // DB OPTIMIZATION: Select strictly the columns needed, NO memory overhead.
            var adviceNotes = await _context.AdviceNotes
                .AsNoTracking()
                .Select(a => new { a.StoreInRecordId, a.ProductionRecordId })
                .ToListAsync();

            var storeInIdsWithDispatch = new HashSet<string>();

            var prodMap = await _context.StoreProductionRecords
                .AsNoTracking()
                .Select(p => new { p.Id, p.StoreInRecordId })
                .ToListAsync();
            
            var prodIdToStoreIn = prodMap.ToDictionary(p => p.Id, p => p.StoreInRecordId);

            foreach (var note in adviceNotes)
            {
                var storeInId = note.StoreInRecordId;
                
                if (string.IsNullOrEmpty(storeInId) && !string.IsNullOrEmpty(note.ProductionRecordId))
                {
                    var firstProdId = note.ProductionRecordId.Split(',').FirstOrDefault()?.Trim();
                    if (firstProdId != null && prodIdToStoreIn.TryGetValue(firstProdId, out var resolved))
                    {
                        storeInId = resolved;
                    }
                }

                if (!string.IsNullOrEmpty(storeInId))
                {
                    storeInIdsWithDispatch.Add(storeInId);
                }
            }

            var combos = await _context.StoreInRecords
                .AsNoTracking()
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
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        [HttpPost("save")]
        public async Task<ActionResult<DeliveryTrackerReport>> SaveTrackerReport(DeliveryTrackerReport report)
        {
            if (string.IsNullOrWhiteSpace(report.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(report.StyleNo))
                return BadRequest("StyleNo is required.");

            var existing = await _context.DeliveryTrackers
                .FirstOrDefaultAsync(r => r.StoreInRecordId == report.StoreInRecordId
                                         && r.FpoNo == report.FpoNo);

            if (existing != null)
            {
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
                if (string.IsNullOrWhiteSpace(report.Id))
                    report.Id = Guid.NewGuid().ToString();

                report.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                _context.DeliveryTrackers.Add(report);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetSavedReports), new { id = report.Id }, report);
            }
        }

        [HttpDelete("saved/{id}")]
        public async Task<IActionResult> DeleteSavedReport(string id)
        {
            var report = await _context.DeliveryTrackers.FindAsync(id);
            if (report == null) return NotFound();

            _context.DeliveryTrackers.Remove(report);
            await _context.SaveChangesAsync();
            return NoContent();
        }

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