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
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetDashboardData()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // --- DEVELOPMENT ---
            var totalJobs = await _context.DevelopmentJobs.CountAsync();
            var totalSubmissions = await _context.Submissions.CountAsync();
            var pendingSubmissions = await _context.Submissions
                .Where(s => s.IsLatestRevision)
                .CountAsync(s => !_context.Approvals.Any(a => a.SubmissionId == s.Id));

            // --- ADMIN / APPROVALS ---
            var totalApprovals = await _context.Approvals.CountAsync();
            var approvedCount = await _context.Approvals.CountAsync(a => a.Status == "Approved");
            var rejectedCount = await _context.Approvals.CountAsync(a => a.Status == "Rejected");
            var pendingApprovals = totalSubmissions - totalApprovals;
            if (pendingApprovals < 0) pendingApprovals = 0;

            // --- STORES ---
            var totalStoreIn = await _context.StoreInRecords.CountAsync();
            var totalInQty = await _context.StoreInRecords.SumAsync(s => s.InQty);
            var todayStoreIn = await _context.StoreInRecords.CountAsync(s => s.CutInDate == today);

            var totalProductionRecords = await _context.StoreProductionRecords.CountAsync();
            var totalIssuedQty = await _context.StoreProductionRecords.SumAsync(p => p.IssueQty);
            var todayProduction = await _context.StoreProductionRecords.CountAsync(p => p.IssueDate == today);

            // Bulk balance summary
            var approvals = await _context.Approvals.Where(a => a.Status == "Approved").ToListAsync();
            var totalBulkApproved = approvals.Sum(a => int.TryParse(a.BulkOrderQty, out var q) ? q : 0);
            var totalBulkReceived = totalInQty;
            var totalBulkRemaining = Math.Max(0, totalBulkApproved - totalBulkReceived);

            // --- QC ---
            var totalCpiReports = await _context.CpiReports.CountAsync();
            var passedCpi = await _context.CpiReports.CountAsync(c => c.InspectionStatus == "Passed");
            var failedCpi = await _context.CpiReports.CountAsync(c => c.InspectionStatus == "Failed");
            var pendingCpi = await _context.CpiReports.CountAsync(c => c.InspectionStatus == "Pending");
            var todayCpi = await _context.CpiReports.CountAsync(c => c.Date == today);

            // --- GATEPASS ---
            var totalAdviceNotes = await _context.AdviceNotes.CountAsync();
            var totalDispatchedQty = await _context.AdviceNotes.SumAsync(a => a.DispatchQty);
            var todayDispatched = await _context.AdviceNotes.CountAsync(a => a.DeliveryDate == today);

            // --- AUDIT ---
            var totalAudits = await _context.AuditRecords.CountAsync();
            var passedAudits = await _context.AuditRecords.CountAsync(a => a.Status == "Pass");
            var failedAudits = await _context.AuditRecords.CountAsync(a => a.Status == "Fail");
            var pendingAudits = await _context.AuditRecords.CountAsync(a => a.Status == "Pending");

            // --- WORKER ---
            var totalDailyOutput = await _context.DailyOutputRecords.CountAsync();
            var todayOutput = await _context.DailyOutputRecords.CountAsync(d => d.Date == today);
            var todayTotalSeating = await _context.DailyOutputRecords
                .Where(d => d.Date == today).SumAsync(d => d.TotalSeating);
            var todayTotalPrinting = await _context.DailyOutputRecords
                .Where(d => d.Date == today).SumAsync(d => d.TotalPrinting);
            var todayTotalCuring = await _context.DailyOutputRecords
                .Where(d => d.Date == today).SumAsync(d => d.TotalCuring);
            var todayTotalChecking = await _context.DailyOutputRecords
                .Where(d => d.Date == today).SumAsync(d => d.TotalChecking);
            var todayTotalPacking = await _context.DailyOutputRecords
                .Where(d => d.Date == today).SumAsync(d => d.TotalPacking);
            var todayTotalDispatch = await _context.DailyOutputRecords
                .Where(d => d.Date == today).SumAsync(d => d.TotalDispatch);

            var totalDowntime = await _context.DowntimeRecords.CountAsync();
            var pendingDowntime = await _context.DowntimeRecords.CountAsync(d => !d.FullyAcknowledged);

            // --- RECENT ACTIVITY (last 5 of each) ---
            var recentStoreIn = await _context.StoreInRecords
                .OrderByDescending(s => s.CutInDate).Take(5)
                .Select(s => new { s.StyleNo, s.CustomerName, s.ScheduleNo, s.InQty, Date = s.CutInDate })
                .ToListAsync();

            var recentDispatches = await _context.AdviceNotes
                .OrderByDescending(a => a.DeliveryDate).Take(5)
                .Select(a => new { a.AdNo, a.StyleNo, a.CustomerName, a.DispatchQty, Date = a.DeliveryDate })
                .ToListAsync();

            var recentAudits = await _context.AuditRecords
                .OrderByDescending(a => a.Date).Take(5)
                .Select(a => new { a.StyleNo, a.CutNo, a.ReleaseQty, a.AuditQty, a.Status, a.Date })
                .ToListAsync();

            return Ok(new
            {
                // Development
                development = new { totalJobs, totalSubmissions, pendingSubmissions },

                // Approvals
                approvals = new { total = totalApprovals, approved = approvedCount, rejected = rejectedCount, pending = pendingApprovals },

                // Stores
                stores = new
                {
                    totalStoreIn, totalInQty, todayStoreIn,
                    totalProductionRecords, totalIssuedQty, todayProduction,
                    bulkApproved = totalBulkApproved,
                    bulkReceived = totalBulkReceived,
                    bulkRemaining = totalBulkRemaining
                },

                // QC
                qc = new { totalCpiReports, passed = passedCpi, failed = failedCpi, pending = pendingCpi, todayCpi },

                // Gatepass
                gatepass = new { totalAdviceNotes, totalDispatchedQty, todayDispatched },

                // Audit
                audit = new { total = totalAudits, passed = passedAudits, failed = failedAudits, pending = pendingAudits },

                // Worker
                worker = new
                {
                    totalDailyOutput, todayOutput,
                    todaySeating = todayTotalSeating, todayPrinting = todayTotalPrinting,
                    todayCuring = todayTotalCuring, todayChecking = todayTotalChecking,
                    todayPacking = todayTotalPacking, todayDispatch = todayTotalDispatch,
                    totalDowntime, pendingDowntime
                },

                // Recent activity
                recent = new { storeIn = recentStoreIn, dispatches = recentDispatches, audits = recentAudits }
            });
        }

        /// <summary>
        /// Per-style pipeline breakdown — shows where each approved style stands
        /// across the entire workflow.
        /// </summary>
        [HttpGet("styles")]
        public async Task<ActionResult> GetStylesOverview()
        {
            // All approved styles
            var approvals = await _context.Approvals
                .Where(a => a.Status == "Approved")
                .ToListAsync();

            if (approvals.Count == 0)
                return Ok(Array.Empty<object>());

            // Preload all downstream data
            var allStoreIn = await _context.StoreInRecords.ToListAsync();
            var allProduction = await _context.StoreProductionRecords.ToListAsync();
            var allCpi = await _context.CpiReports.ToListAsync();
            var allAdviceNotes = await _context.AdviceNotes.ToListAsync();
            var allAudits = await _context.AuditRecords.ToListAsync();
            var allDailyOutput = await _context.DailyOutputRecords.ToListAsync();

            var styles = approvals.Select(approval =>
            {
                var bulkQty = int.TryParse(approval.BulkOrderQty, out var bq) ? bq : 0;

                // Store-In for this submission
                var storeIns = allStoreIn.Where(s => s.SubmissionId == approval.SubmissionId).ToList();
                var totalReceived = storeIns.Sum(s => s.InQty);
                var totalCuts = storeIns.Sum(s => s.Cuts?.Count ?? 0);

                // Production
                var storeInIds = storeIns.Select(s => s.Id).ToHashSet();
                var productions = allProduction.Where(p => storeInIds.Contains(p.StoreInRecordId)).ToList();
                var totalIssued = productions.Sum(p => p.IssueQty);

                // QC
                var cpiReports = allCpi.Where(c => storeInIds.Contains(c.StoreInRecordId)).ToList();
                var qcPassed = cpiReports.Count(c => c.InspectionStatus == "Passed");
                var qcFailed = cpiReports.Count(c => c.InspectionStatus == "Failed");
                var qcPending = cpiReports.Count(c => c.InspectionStatus == "Pending");

                // Gatepass
                var dispatches = allAdviceNotes.Where(a => storeInIds.Contains(a.StoreInRecordId)).ToList();
                var totalDispatched = dispatches.Sum(a => a.DispatchQty);

                // Audit
                var audits = allAudits.Where(a => storeInIds.Contains(a.StoreInRecordId)).ToList();
                var auditPassed = audits.Count(a => a.Status == "Pass");
                var auditFailed = audits.Count(a => a.Status == "Fail");

                // Worker output
                var workerRecords = allDailyOutput.Where(d => storeInIds.Contains(d.StoreInRecordId)).ToList();
                var totalWorkerOutput = workerRecords.Sum(d => d.TotalSeating + d.TotalPrinting + d.TotalCuring + d.TotalChecking + d.TotalPacking + d.TotalDispatch);

                // Pipeline stage
                var remainingBulk = Math.Max(0, bulkQty - totalReceived);
                string stage;
                if (totalDispatched >= bulkQty && bulkQty > 0) stage = "Completed";
                else if (totalDispatched > 0) stage = "Dispatching";
                else if (totalIssued > 0) stage = "In Production";
                else if (qcPassed > 0) stage = "QC Passed";
                else if (totalReceived > 0) stage = "Received";
                else stage = "Approved";

                return new
                {
                    styleNo = approval.StyleNo,
                    customerName = approval.CustomerName,
                    scheduleNo = storeIns.FirstOrDefault()?.ScheduleNo ?? "-",
                    bulkQty,
                    stage,

                    // Store In
                    storeInCount = storeIns.Count,
                    totalReceived,
                    remainingBulk,
                    receivedPct = bulkQty > 0 ? Math.Round((double)totalReceived / bulkQty * 100, 1) : 0,
                    totalCuts,

                    // QC
                    qcTotal = cpiReports.Count,
                    qcPassed,
                    qcFailed,
                    qcPending,

                    // Production
                    productionCount = productions.Count,
                    totalIssued,

                    // Dispatch
                    dispatchCount = dispatches.Count,
                    totalDispatched,
                    dispatchedPct = bulkQty > 0 ? Math.Round((double)totalDispatched / bulkQty * 100, 1) : 0,

                    // Audit
                    auditTotal = audits.Count,
                    auditPassed,
                    auditFailed,

                    // Worker
                    workerEntries = workerRecords.Count,
                    totalWorkerOutput,
                };
            })
            .OrderByDescending(s => s.stage == "Completed" ? 0 : 1) // Completed at bottom
            .ThenByDescending(s => s.totalReceived) // Most active first
            .ToList();

            return Ok(styles);
        }
    }
}