using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;

namespace CpPrinting.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private const string DashboardStringCollation = "SQL_Latin1_General_CP1_CI_AS";

        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        private static string GetDashboardToday()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("yyyy-MM-dd");
            }
            catch
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("yyyy-MM-dd");
                }
                catch
                {
                    return DateTime.Now.ToString("yyyy-MM-dd");
                }
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetDashboardData()
        {
            var today = GetDashboardToday();

            // EF Core DbContext is NOT thread-safe — all queries must be sequential.
            // AsNoTracking() still gives a significant speed boost on read-only queries.

            // --- Development ---
            var totalJobs          = await _context.DevelopmentJobs.AsNoTracking().CountAsync();
            var totalSubmissions   = await _context.Submissions.AsNoTracking().CountAsync();
            var pendingSubmissions = await _context.Submissions.AsNoTracking()
                                        .Where(s => s.IsLatestRevision &&
                                                    !_context.Approvals.Any(a => a.SubmissionId == s.Id))
                                        .CountAsync();

            // --- Approvals ---
            var totalApprovals = await _context.Approvals.AsNoTracking().CountAsync();
            var approvedCount  = await _context.Approvals.AsNoTracking().CountAsync(a => a.Status == "Approved");
            var rejectedCount  = await _context.Approvals.AsNoTracking().CountAsync(a => a.Status == "Rejected");

            // --- Stores: Store-In ---
            var totalStoreIn = await _context.StoreInRecords.AsNoTracking().CountAsync();
            var totalInQty   = await _context.StoreInRecords.AsNoTracking().SumAsync(s => s.InQty);
            var todayStoreIn = await _context.StoreInRecords.AsNoTracking().CountAsync(s => s.CutInDate == today);

            // --- Stores: Production ---
            var totalProdRecords = await _context.StoreProductionRecords.AsNoTracking().CountAsync();
            var totalIssuedQty   = await _context.StoreProductionRecords.AsNoTracking().SumAsync(p => p.IssueQty);
            var todayProduction  = await _context.StoreProductionRecords.AsNoTracking().CountAsync(p => p.IssueDate == today);

            // --- Bulk balance ---
            var approvedBulkStrings = await _context.Approvals.AsNoTracking()
                .Where(a => a.Status == "Approved")
                .Select(a => a.BulkOrderQty)
                .ToListAsync();
            var totalBulkApproved  = approvedBulkStrings.Sum(q => int.TryParse(q, out var n) ? n : 0);
            var totalBulkReceived  = totalInQty;
            var totalBulkRemaining = Math.Max(0, totalBulkApproved - totalBulkReceived);

            // --- QC ---
            var totalCpiReports = await _context.CpiReports.AsNoTracking().CountAsync();
            var passedCpi       = await _context.CpiReports.AsNoTracking().CountAsync(c => c.InspectionStatus == "Passed");
            var failedCpi       = await _context.CpiReports.AsNoTracking().CountAsync(c => c.InspectionStatus == "Failed");
            var pendingCpi      = await _context.CpiReports.AsNoTracking().CountAsync(c => c.InspectionStatus == "Pending");
            var todayCpi        = await _context.CpiReports.AsNoTracking().CountAsync(c => c.Date == today);

            // --- Gatepass ---
            var totalAdviceNotes   = await _context.AdviceNotes.AsNoTracking().CountAsync();
            var totalDispatchedQty = await _context.AdviceNotes.AsNoTracking().SumAsync(a => a.DispatchQty);
            var todayDispatched    = await _context.AdviceNotes.AsNoTracking().CountAsync(a => a.DeliveryDate == today);

            // --- Audit ---
            var totalAudits   = await _context.AuditRecords.AsNoTracking().CountAsync();
            var passedAudits  = await _context.AuditRecords.AsNoTracking().CountAsync(a => a.Status == "Pass");
            var failedAudits  = await _context.AuditRecords.AsNoTracking().CountAsync(a => a.Status == "Fail");
            var pendingAudits = await _context.AuditRecords.AsNoTracking().CountAsync(a => a.Status == "Pending");

            // --- Worker ---
            var totalDailyOutput = await _context.DailyOutputRecords.AsNoTracking().CountAsync();
            var todayOutputCount = await _context.DailyOutputRecords.AsNoTracking().CountAsync(d => d.Date == today);
            var todaySeating     = await _context.DailyOutputRecords.AsNoTracking().Where(d => d.Date == today).SumAsync(d => d.TotalSeating);
            var todayPrinting    = await _context.DailyOutputRecords.AsNoTracking().Where(d => d.Date == today).SumAsync(d => d.TotalPrinting);
            var todayCuring      = await _context.DailyOutputRecords.AsNoTracking().Where(d => d.Date == today).SumAsync(d => d.TotalCuring);
            var todayChecking    = await _context.DailyOutputRecords.AsNoTracking().Where(d => d.Date == today).SumAsync(d => d.TotalChecking);
            var todayPacking     = await _context.DailyOutputRecords.AsNoTracking().Where(d => d.Date == today).SumAsync(d => d.TotalPacking);
            var todayDispatch    = await _context.DailyOutputRecords.AsNoTracking().Where(d => d.Date == today).SumAsync(d => d.TotalDispatch);
            var totalDowntime    = await _context.DowntimeRecords.AsNoTracking().CountAsync();
            var pendingDowntime  = await _context.DowntimeRecords.AsNoTracking().CountAsync(d => !d.FullyAcknowledged);

            // --- Recent activity (only needed columns) ---
            var recentStoreIn = await _context.StoreInRecords.AsNoTracking()
                .OrderByDescending(s => s.CutInDate)
                .Take(5)
                .Select(s => new { s.StyleNo, s.CustomerName, s.ScheduleNo, s.InQty, Date = s.CutInDate })
                .ToListAsync();

            var recentDispatches = await _context.AdviceNotes.AsNoTracking()
                .OrderByDescending(a => a.DeliveryDate)
                .Take(5)
                .Select(a => new { a.AdNo, a.StyleNo, a.CustomerName, a.DispatchQty, Date = a.DeliveryDate })
                .ToListAsync();

            var recentAudits = await _context.AuditRecords.AsNoTracking()
                .OrderByDescending(a => a.Date)
                .Take(5)
                .Select(a => new { a.StyleNo, a.CutNo, a.ReleaseQty, a.AuditQty, a.Status, a.Date })
                .ToListAsync();

            var pendingApprovals = Math.Max(0, totalSubmissions - totalApprovals);

            return Ok(new
            {
                development = new
                {
                    totalJobs,
                    totalSubmissions,
                    pendingSubmissions
                },
                approvals = new
                {
                    total    = totalApprovals,
                    approved = approvedCount,
                    rejected = rejectedCount,
                    pending  = pendingApprovals
                },
                stores = new
                {
                    totalStoreIn,
                    totalInQty,
                    todayStoreIn,
                    totalProductionRecords = totalProdRecords,
                    totalIssuedQty,
                    todayProduction,
                    bulkApproved  = totalBulkApproved,
                    bulkReceived  = totalBulkReceived,
                    bulkRemaining = totalBulkRemaining
                },
                qc = new
                {
                    totalCpiReports,
                    passed  = passedCpi,
                    failed  = failedCpi,
                    pending = pendingCpi,
                    todayCpi
                },
                gatepass = new
                {
                    totalAdviceNotes,
                    totalDispatchedQty,
                    todayDispatched
                },
                audit = new
                {
                    total   = totalAudits,
                    passed  = passedAudits,
                    failed  = failedAudits,
                    pending = pendingAudits
                },
                worker = new
                {
                    totalDailyOutput,
                    todayOutput   = todayOutputCount,
                    todaySeating,
                    todayPrinting,
                    todayCuring,
                    todayChecking,
                    todayPacking,
                    todayDispatch,
                    totalDowntime,
                    pendingDowntime
                },
                recent = new
                {
                    storeIn    = recentStoreIn,
                    dispatches = recentDispatches,
                    audits     = recentAudits
                }
            });
        }

        /// <summary>
        /// Per-style pipeline overview.
        /// All aggregation done in the database — no in-memory LINQ on full tables.
        /// Queries are sequential to respect EF Core's single-threaded DbContext.
        /// </summary>
        [HttpGet("styles")]
        public async Task<ActionResult> GetStylesOverview()
        {
            var approvals = await _context.Approvals.AsNoTracking()
                .Where(a => a.Status == "Approved")
                .Select(a => new
                {
                    a.SubmissionId,
                    a.StyleNo,
                    a.CustomerName,
                    a.BulkOrderQty
                })
                .ToListAsync();

            if (approvals.Count == 0)
                return Ok(Array.Empty<object>());

            var submissionIds = approvals.Select(a => a.SubmissionId).ToList();

            // Store-in summary per submission
            var storeInSummary = await _context.StoreInRecords.AsNoTracking()
                .Where(s => submissionIds.Contains(EF.Functions.Collate(s.SubmissionId, DashboardStringCollation)))
                .GroupBy(s => s.SubmissionId)
                .Select(g => new
                {
                    SubmissionId  = g.Key,
                    TotalReceived = g.Sum(s => s.InQty),
                    StoreInCount  = g.Count(),
                    ScheduleNo    = g.Select(s => s.ScheduleNo).FirstOrDefault() ?? ""
                })
                .ToListAsync();

            // StoreIn IDs needed for downstream joins
            var storeInRows = await _context.StoreInRecords.AsNoTracking()
                .Where(s => submissionIds.Contains(EF.Functions.Collate(s.SubmissionId, DashboardStringCollation)))
                .Select(s => new { s.SubmissionId, s.Id })
                .ToListAsync();

            var storeInSummaryDict     = storeInSummary.ToDictionary(x => x.SubmissionId);
            var storeInIdsBySubmission = storeInRows
                .GroupBy(x => x.SubmissionId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
            var allStoreInIds          = storeInRows.Select(x => x.Id).ToList();

            if (allStoreInIds.Count == 0)
            {
                return Ok(approvals.Select(a => new
                {
                    styleNo = a.StyleNo, customerName = a.CustomerName,
                    scheduleNo = "", bulkQty = int.TryParse(a.BulkOrderQty, out var bq) ? bq : 0,
                    stage = "Approved",
                    storeInCount = 0, totalReceived = 0,
                    remainingBulk = int.TryParse(a.BulkOrderQty, out var bq2) ? bq2 : 0,
                    receivedPct = 0.0, totalCuts = 0,
                    qcTotal = 0, qcPassed = 0, qcFailed = 0, qcPending = 0,
                    productionCount = 0, totalIssued = 0,
                    dispatchCount = 0, totalDispatched = 0, dispatchedPct = 0.0,
                    auditTotal = 0, auditPassed = 0, auditFailed = 0,
                    workerEntries = 0, totalWorkerOutput = 0
                }));
            }

            // Sequential DB aggregations — one await at a time
            var productionData = await _context.StoreProductionRecords.AsNoTracking()
                .Where(p => allStoreInIds.Contains(EF.Functions.Collate(p.StoreInRecordId, DashboardStringCollation)))
                .GroupBy(p => p.StoreInRecordId)
                .Select(g => new { StoreInRecordId = g.Key, TotalIssued = g.Sum(p => p.IssueQty), Count = g.Count() })
                .ToListAsync();

            var cpiData = await _context.CpiReports.AsNoTracking()
                .Where(c => allStoreInIds.Contains(EF.Functions.Collate(c.StoreInRecordId, DashboardStringCollation)))
                .GroupBy(c => c.StoreInRecordId)
                .Select(g => new
                {
                    StoreInRecordId = g.Key,
                    Total   = g.Count(),
                    Passed  = g.Count(c => c.InspectionStatus == "Passed"),
                    Failed  = g.Count(c => c.InspectionStatus == "Failed"),
                    Pending = g.Count(c => c.InspectionStatus == "Pending")
                })
                .ToListAsync();

            var dispatchData = await _context.AdviceNotes.AsNoTracking()
                .Where(a => allStoreInIds.Contains(EF.Functions.Collate(a.StoreInRecordId, DashboardStringCollation)))
                .GroupBy(a => a.StoreInRecordId)
                .Select(g => new { StoreInRecordId = g.Key, TotalDispatched = g.Sum(a => a.DispatchQty), Count = g.Count() })
                .ToListAsync();

            var auditData = await _context.AuditRecords.AsNoTracking()
                .Where(a => allStoreInIds.Contains(EF.Functions.Collate(a.StoreInRecordId, DashboardStringCollation)))
                .GroupBy(a => a.StoreInRecordId)
                .Select(g => new
                {
                    StoreInRecordId = g.Key,
                    Total  = g.Count(),
                    Passed = g.Count(a => a.Status == "Pass"),
                    Failed = g.Count(a => a.Status == "Fail")
                })
                .ToListAsync();

            var workerData = await _context.DailyOutputRecords.AsNoTracking()
                .Where(d => allStoreInIds.Contains(EF.Functions.Collate(d.StoreInRecordId, DashboardStringCollation)))
                .GroupBy(d => d.StoreInRecordId)
                .Select(g => new
                {
                    StoreInRecordId = g.Key,
                    Entries     = g.Count(),
                    TotalOutput = g.Sum(d => d.TotalSeating + d.TotalPrinting + d.TotalCuring +
                                             d.TotalChecking + d.TotalPacking + d.TotalDispatch)
                })
                .ToListAsync();

            var cutData = await _context.CutRecords.AsNoTracking()
                .Where(c => allStoreInIds.Contains(EF.Functions.Collate(c.StoreInRecordId, DashboardStringCollation)))
                .GroupBy(c => c.StoreInRecordId)
                .Select(g => new { StoreInRecordId = g.Key, Count = g.Count() })
                .ToListAsync();

            // Build lookups — all in memory now
            var productionByStoreIn = productionData.ToDictionary(x => x.StoreInRecordId);
            var cpiByStoreIn        = cpiData.ToDictionary(x => x.StoreInRecordId);
            var dispatchByStoreIn   = dispatchData.ToDictionary(x => x.StoreInRecordId);
            var auditByStoreIn      = auditData.ToDictionary(x => x.StoreInRecordId);
            var workerByStoreIn     = workerData.ToDictionary(x => x.StoreInRecordId);
            var cutsByStoreIn       = cutData.ToDictionary(x => x.StoreInRecordId);

            var styles = approvals.Select(approval =>
            {
                var bulkQty    = int.TryParse(approval.BulkOrderQty, out var bq) ? bq : 0;
                var storeInIds = storeInIdsBySubmission.GetValueOrDefault(approval.SubmissionId, new List<string>());
                var summary    = storeInSummaryDict.GetValueOrDefault(approval.SubmissionId);

                var totalReceived = summary?.TotalReceived ?? 0;
                var storeInCount  = summary?.StoreInCount  ?? 0;
                var scheduleNo    = summary?.ScheduleNo    ?? "-";

                int totalIssued = 0, prodCount = 0;
                int qcTotal = 0, qcPassed = 0, qcFailed = 0, qcPending = 0;
                int totalDispatched = 0, dispatchCount = 0;
                int auditTotal = 0, auditPassed = 0, auditFailed = 0;
                int workerEntries = 0, totalWorkerOutput = 0;
                int totalCuts = 0;

                foreach (var sid in storeInIds)
                {
                    if (productionByStoreIn.TryGetValue(sid, out var prod))
                    { totalIssued += prod.TotalIssued; prodCount += prod.Count; }

                    if (cpiByStoreIn.TryGetValue(sid, out var cpi))
                    { qcTotal += cpi.Total; qcPassed += cpi.Passed; qcFailed += cpi.Failed; qcPending += cpi.Pending; }

                    if (dispatchByStoreIn.TryGetValue(sid, out var disp))
                    { totalDispatched += disp.TotalDispatched; dispatchCount += disp.Count; }

                    if (auditByStoreIn.TryGetValue(sid, out var aud))
                    { auditTotal += aud.Total; auditPassed += aud.Passed; auditFailed += aud.Failed; }

                    if (workerByStoreIn.TryGetValue(sid, out var wrk))
                    { workerEntries += wrk.Entries; totalWorkerOutput += wrk.TotalOutput; }

                    if (cutsByStoreIn.TryGetValue(sid, out var cuts))
                    { totalCuts += cuts.Count; }
                }

                var remainingBulk = Math.Max(0, bulkQty - totalReceived);

                string stage;
                if (totalDispatched >= bulkQty && bulkQty > 0) stage = "Completed";
                else if (totalDispatched > 0)                  stage = "Dispatching";
                else if (totalIssued > 0)                      stage = "In Production";
                else if (qcPassed > 0)                         stage = "QC Passed";
                else if (totalReceived > 0)                    stage = "Received";
                else                                           stage = "Approved";

                return new
                {
                    styleNo      = approval.StyleNo,
                    customerName = approval.CustomerName,
                    scheduleNo,
                    bulkQty,
                    stage,
                    storeInCount,
                    totalReceived,
                    remainingBulk,
                    receivedPct   = bulkQty > 0 ? Math.Round((double)totalReceived / bulkQty * 100, 1) : 0.0,
                    totalCuts,
                    qcTotal, qcPassed, qcFailed, qcPending,
                    productionCount = prodCount,
                    totalIssued,
                    dispatchCount,
                    totalDispatched,
                    dispatchedPct = bulkQty > 0 ? Math.Round((double)totalDispatched / bulkQty * 100, 1) : 0.0,
                    auditTotal, auditPassed, auditFailed,
                    workerEntries, totalWorkerOutput
                };
            })
            .OrderByDescending(s => s.stage == "Completed" ? 0 : 1)
            .ThenByDescending(s => s.totalReceived)
            .ToList();

            return Ok(styles);
        }
    }
}