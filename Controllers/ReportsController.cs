using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("filter-options")]
        public async Task<ActionResult<ReportFilterOptionsDto>> GetFilterOptions(
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? section = null)
        {
            var range = ResolveDateRange(dateFrom, dateTo);
            var toEnd = ToEndOfDay(range.To);
            var resolvedSection = NormalizeSection(section ?? string.Empty) ?? "Overview";

            var options = new ReportFilterOptionsDto
            {
                Sections = new List<string> { "Overview", "Development", "Stores", "QC", "Gatepass", "Worker", "Users" },
                Roles = RolesForSection(resolvedSection),
                Statuses = StatusesForSection(resolvedSection),
                TimeSlots = WorkerTimeSlots()
            };

            var logs = await _context.ActivityLogs
                .AsNoTracking()
                .Where(l => !string.IsNullOrEmpty(l.Timestamp) && string.Compare(l.Timestamp, range.From) >= 0 && string.Compare(l.Timestamp, toEnd) <= 0)
                .ToListAsync();

            switch (resolvedSection)
            {
                case "Development":
                {
                    var rows = await _context.SampleStyles.AsNoTracking()
                        .Where(s => !string.IsNullOrEmpty(s.CreatedAt) && string.Compare(s.CreatedAt, range.From) >= 0 && string.Compare(s.CreatedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.UpdatedAt) && string.Compare(s.UpdatedAt, range.From) >= 0 && string.Compare(s.UpdatedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.SubmittedAt) && string.Compare(s.SubmittedAt, range.From) >= 0 && string.Compare(s.SubmittedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.AdminActionAt) && string.Compare(s.AdminActionAt, range.From) >= 0 && string.Compare(s.AdminActionAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.ClientApprovedAt) && string.Compare(s.ClientApprovedAt, range.From) >= 0 && string.Compare(s.ClientApprovedAt, toEnd) <= 0)
                        .ToListAsync();
                    options.Styles = Distinct(rows.Select(s => s.StyleNo));
                    options.Customers = Distinct(rows.Select(s => s.Customer));
                    options.Schedules = new List<string>();
                    options.Employees = Distinct(rows.SelectMany(s => DevelopmentPeople(s)).Concat(logs.Where(l => Same(l.UserRole, "Developer") || Same(l.UserRole, "Admin")).Select(l => l.UserName)));
                    break;
                }
                case "Stores":
                {
                    var rows = await _context.StoreInRecords.AsNoTracking()
                        .Where(s => !string.IsNullOrEmpty(s.CutInDate) && string.Compare(s.CutInDate, range.From) >= 0 && string.Compare(s.CutInDate, toEnd) <= 0)
                        .ToListAsync();
                    options.Styles = Distinct(rows.Select(s => s.StyleNo));
                    options.Customers = Distinct(rows.Select(s => s.CustomerName));
                    options.Schedules = Distinct(rows.Select(s => s.ScheduleNo));
                    options.Employees = Distinct(logs.Where(l => Same(l.UserRole, "Stores") || Same(l.Entity, "StoreIn") || Same(l.Entity, "Production") || Same(l.Entity, "StoreProduction"))
                        .Select(l => l.UserName));
                    break;
                }
                case "QC":
                {
                    var rows = await _context.CpiReports.AsNoTracking()
                        .Where(c => !string.IsNullOrEmpty(c.SummaryDate) && string.Compare(c.SummaryDate, range.From) >= 0 && string.Compare(c.SummaryDate, toEnd) <= 0 || !string.IsNullOrEmpty(c.Date) && string.Compare(c.Date, range.From) >= 0 && string.Compare(c.Date, toEnd) <= 0)
                        .ToListAsync();
                    options.Styles = Distinct(rows.Select(c => c.StyleNo));
                    options.Customers = Distinct(rows.Select(c => c.Customer));
                    options.Schedules = Distinct(rows.Select(c => c.ScheduleNo));
                    options.Employees = Distinct(rows.SelectMany(c => new[] { c.CheckedBy, c.CpiAuditor }).Concat(logs.Where(l => Same(l.UserRole, "QC")).Select(l => l.UserName)));
                    break;
                }
                case "Gatepass":
                {
                    var rows = await _context.AdviceNotes.AsNoTracking()
                        .Where(a => !string.IsNullOrEmpty(a.DeliveryDate) && string.Compare(a.DeliveryDate, range.From) >= 0 && string.Compare(a.DeliveryDate, toEnd) <= 0)
                        .ToListAsync();
                    options.Styles = Distinct(rows.Select(a => a.StyleNo));
                    options.Customers = Distinct(rows.Select(a => a.CustomerName));
                    options.Schedules = Distinct(rows.Select(a => a.ScheduleNo));
                    options.Employees = Distinct(rows.SelectMany(a => new[] { a.PrepByName, a.AuthByName, a.ReceivedByName }).Concat(logs.Where(l => Same(l.UserRole, "Gatepass")).Select(l => l.UserName)));
                    break;
                }
                case "Worker":
                {
                    var rows = await _context.DailyOutputRecords.AsNoTracking()
                        .Where(d => !string.IsNullOrEmpty(d.Date) && string.Compare(d.Date, range.From) >= 0 && string.Compare(d.Date, toEnd) <= 0 || !string.IsNullOrEmpty(d.CompletedAt) && string.Compare(d.CompletedAt, range.From) >= 0 && string.Compare(d.CompletedAt, toEnd) <= 0)
                        .ToListAsync();
                    options.Styles = Distinct(rows.Select(d => d.StyleNo));
                    options.Customers = Distinct(rows.Select(d => d.CustomerName));
                    options.Schedules = await LoadWorkerSchedules(rows.Select(d => d.StoreInRecordId).Distinct().ToList());
                    options.Employees = Distinct(rows.SelectMany(d => new[] { d.WorkerName, d.CompletedBy }).Concat(logs.Where(l => Same(l.UserRole, "Worker")).Select(l => l.UserName)));
                    break;
                }
                default:
                {
                    options.Styles = new List<string>();
                    options.Customers = new List<string>();
                    options.Schedules = new List<string>();
                    options.Employees = Distinct(logs.Select(l => l.UserName));
                    break;
                }
            }

            return Ok(options);
        }

        [HttpGet("overview")]
        public async Task<ActionResult<ManagementOverviewDto>> GetOverview(
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null)
        {
            var range = ResolveDateRange(dateFrom, dateTo);
            var snapshot = await LoadSnapshot(range.From, range.To);
            var departments = BuildDepartmentCards(snapshot);

            return Ok(new ManagementOverviewDto
            {
                GeneratedAt = NowStamp(),
                DateFrom = range.From,
                DateTo = range.To,
                Title = "Management Overview",
                HeadlineCards = BuildHeadlineCards(snapshot),
                Departments = departments,
                Charts = new List<ReportChartDto>
                {
                    BarChart("department-workload", "Department workload", "Volume handled by each department in the selected period.", departments.Select(d => Point(d.Section, d.WorkloadValue, d.PrimaryMetric))),
                    PieChart("pending-work", "Pending work by department", "Current unfinished work count/quantity that needs attention.", departments.Select(d => Point(d.Section, d.PendingValue, d.PendingLabel))),
                    LineChart("company-throughput", "Operational movement trend", "Daily movement across development, stores, QC, gatepass and worker output.", BuildOverviewTrend(snapshot, range.From, range.To))
                }
            });
        }

        // Backward compatible route if the earlier frontend still calls /dashboard.
        [HttpGet("dashboard")]
        public Task<ActionResult<ManagementOverviewDto>> GetDashboard([FromQuery] string? dateFrom = null, [FromQuery] string? dateTo = null)
        {
            return GetOverview(dateFrom, dateTo);
        }

        [HttpGet("sections/{section}")]
        public async Task<ActionResult<SectionReportDto>> GetSectionReport(
            string section,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? status = null,
            [FromQuery] string? employee = null,
            [FromQuery] string? role = null,
            [FromQuery] string? styleNo = null,
            [FromQuery] string? customer = null,
            [FromQuery] string? scheduleNo = null,
            [FromQuery] string? timeSlot = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var resolvedSection = NormalizeSection(section);
            if (resolvedSection == null)
                return BadRequest("Unknown report section.");

            var range = ResolveDateRange(dateFrom, dateTo);
            var filter = new ReportQueryFilter
            {
                DateFrom = range.From,
                DateTo = range.To,
                Status = Clean(status),
                Employee = Clean(employee),
                Role = Clean(role),
                StyleNo = Clean(styleNo),
                Customer = Clean(customer),
                ScheduleNo = Clean(scheduleNo),
                TimeSlot = Clean(timeSlot),
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 25, 200)
            };

            var report = resolvedSection switch
            {
                "Development" => await BuildDevelopmentReport(filter),
                "Stores"      => await BuildStoresReport(filter),
                "QC"          => await BuildQcReport(filter),
                "Gatepass"    => await BuildGatepassReport(filter),
                "Worker"      => await BuildWorkerReport(filter),
                _             => null
            };

            if (report == null)
                return BadRequest("Unknown report section.");

            ApplyReportEmployeeFilters(report, filter);

            report.GeneratedAt = NowStamp();
            report.DateFrom = range.From;
            report.DateTo = range.To;
            report.AppliedFilters = filter.ToAppliedFilters();
            return Ok(report);
        }

        [HttpGet("employee-performance")]
        public async Task<ActionResult<IEnumerable<EmployeePerformanceDto>>> GetEmployeePerformance(
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? section = null)
        {
            var range = ResolveDateRange(dateFrom, dateTo);
            var snapshot = await LoadSnapshot(range.From, range.To);
            var rows = BuildEmployeePerformance(snapshot);

            var resolvedSection = NormalizeSection(section ?? "");
            if (resolvedSection != null)
                rows = rows.Where(r => Same(r.Department, resolvedSection)).ToList();

            return Ok(rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.OutputQty).ToList());
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserReportRowDto>>> GetUsersReport([FromQuery] string? role = null)
        {
            var roleFilter = Clean(role);
            var usersQuery = _context.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(roleFilter))
                usersQuery = usersQuery.Where(u => u.Role == roleFilter);

            var users = await usersQuery.OrderBy(u => u.Name).ToListAsync();
            var logs = await _context.ActivityLogs.AsNoTracking().ToListAsync();

            var rows = users.Select(u =>
            {
                var userLogs = logs.Where(l => LogBelongsToUser(l, u)).ToList();
                var lastActivity = userLogs.OrderByDescending(l => l.Timestamp).FirstOrDefault();
                var lastLogin = userLogs.Where(l => l.Action == "Login").OrderByDescending(l => l.Timestamp).FirstOrDefault();

                return new UserReportRowDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    Role = u.Role,
                    ActivityCount = userLogs.Count,
                    LastLogin = lastLogin?.Timestamp ?? string.Empty,
                    LastActivity = lastActivity?.Timestamp ?? string.Empty
                };
            }).ToList();

            return Ok(rows);
        }

        private async Task<SectionReportDto> BuildDevelopmentReport(ReportQueryFilter filter)
        {
            var toEnd = ToEndOfDay(filter.DateTo);
            var styles = await _context.SampleStyles
                .AsNoTracking()
                .Where(s => !string.IsNullOrEmpty(s.CreatedAt) && string.Compare(s.CreatedAt, filter.DateFrom) >= 0 && string.Compare(s.CreatedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.UpdatedAt) && string.Compare(s.UpdatedAt, filter.DateFrom) >= 0 && string.Compare(s.UpdatedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.SubmittedAt) && string.Compare(s.SubmittedAt, filter.DateFrom) >= 0 && string.Compare(s.SubmittedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.AdminActionAt) && string.Compare(s.AdminActionAt, filter.DateFrom) >= 0 && string.Compare(s.AdminActionAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.ClientApprovedAt) && string.Compare(s.ClientApprovedAt, filter.DateFrom) >= 0 && string.Compare(s.ClientApprovedAt, toEnd) <= 0)
                .ToListAsync();

            styles = styles
                .Where(s => Match(filter.StyleNo, s.StyleNo))
                .Where(s => Match(filter.Customer, s.Customer))
                .Where(s => Match(filter.Status, ResolveDevelopmentStatus(s)))
                .Where(s => string.IsNullOrWhiteSpace(filter.Employee) || DevelopmentPeople(s).Any(p => Match(filter.Employee, p)))
                .ToList();

            var allRows = styles.Select(s =>
            {
                var latestRevision = s.Revisions?.OrderByDescending(r => r.RevisionNo).FirstOrDefault();
                var statusText = ResolveDevelopmentStatus(s);
                return Row(new Dictionary<string, object>
                {
                    ["createdAt"] = s.CreatedAt,
                    ["styleNo"] = s.StyleNo,
                    ["customer"] = s.Customer,
                    ["season"] = s.Season,
                    ["component"] = s.Component,
                    ["bodyColour"] = s.BodyColour,
                    ["printColour"] = s.PrintColour,
                    ["printingTechnique"] = s.PrintingTechnique,
                    ["washingStandard"] = s.WashingStandard,
                    ["clientApproved"] = YesNo(s.ClientApproved),
                    ["clientApprovedAt"] = s.ClientApprovedAt ?? string.Empty,
                    ["clientApprovedBy"] = s.ClientApprovedBy ?? string.Empty,
                    ["submittedToAdmin"] = YesNo(s.SubmittedToAdmin || !string.IsNullOrWhiteSpace(s.SubmittedAt)),
                    ["submittedAt"] = s.SubmittedAt ?? string.Empty,
                    ["adminStatus"] = s.AdminStatus,
                    ["adminRemarks"] = s.AdminRemarks ?? string.Empty,
                    ["adminActionBy"] = s.AdminActionBy ?? string.Empty,
                    ["adminActionAt"] = s.AdminActionAt ?? string.Empty,
                    ["bulkQty"] = ParseInt(s.BulkQty),
                    ["revisionCount"] = s.Revisions?.Count ?? 0,
                    ["latestRevision"] = latestRevision == null ? string.Empty : $"Rev {latestRevision.RevisionNo}: {latestRevision.Comment}",
                    ["currentStage"] = statusText,
                    ["pendingWork"] = statusText == "Admin approved" ? "Completed" : statusText
                });
            }).OrderByDescending(r => Convert.ToString(r["createdAt"])).ToList();

            var approved = styles.Count(s => Same(s.AdminStatus, "Approved"));
            var rejected = styles.Count(s => Same(s.AdminStatus, "Rejected"));
            var pending = styles.Count(s => Same(ResolveDevelopmentStatus(s), "Awaiting admin"));
            var notSubmitted = styles.Count(s => Same(ResolveDevelopmentStatus(s), "Sample created") || Same(ResolveDevelopmentStatus(s), "Client approved, not submitted"));
            var clientApproved = styles.Count(s => s.ClientApproved);
            var submitted = styles.Count(s => s.SubmittedToAdmin || !string.IsNullOrWhiteSpace(s.SubmittedAt));
            var revisions = styles.Sum(s => s.Revisions?.Count ?? 0);

            return SectionReport(
                "Development",
                "Development Performance Report",
                "Samples, style submissions, approvals, rejections, pending styles and revision history for the selected period.",
                new List<MetricCardDto>
                {
                    Metric("Samples created", styles.Count, "All sample/style records"),
                    Metric("Submitted", submitted, "Styles submitted to admin"),
                    Metric("Client approved", clientApproved, "Styles approved by client"),
                    Metric("Admin approved", approved, "Final approved styles"),
                    Metric("Rejected", rejected, "Rejected by admin"),
                    Metric("Pending work", pending + notSubmitted, "Not yet fully approved")
                },
                new List<ReportChartDto>
                {
                    LineChart("development-trend", "Development trend", "Daily created, submitted and approved style movement.", BuildDevelopmentTrend(styles, filter.DateFrom, filter.DateTo)),
                    PieChart("development-status", "Development status split", "Approved, rejected, pending and not-submitted style count.", new [] { Point("Approved", approved), Point("Rejected", rejected), Point("Pending admin", pending), Point("Not submitted", notSubmitted) }),
                    BarChart("development-revisions", "Styles with most revisions", "Top styles by revision/correction count.", styles.OrderByDescending(s => s.Revisions?.Count ?? 0).Take(10).Select(s => Point(s.StyleNo, s.Revisions?.Count ?? 0, s.Customer)))
                },
                BuildDevelopmentEmployeeRows(styles),
                new List<ReportColumnDto>
                {
                    Col("createdAt", "Created"), Col("styleNo", "Style No"), Col("customer", "Customer"), Col("season", "Season"),
                    Col("component", "Component"), Col("bodyColour", "Body Colour"), Col("printColour", "Print Colour"), Col("printingTechnique", "Print Technique"),
                    Col("clientApproved", "Client Approved"), Col("submittedToAdmin", "Submitted"), Col("adminStatus", "Admin Status"),
                    Col("bulkQty", "Bulk Qty", "number"), Col("revisionCount", "Revisions", "number"), Col("adminActionBy", "Admin Action By"),
                    Col("adminActionAt", "Admin Action At"), Col("latestRevision", "Latest Revision"), Col("pendingWork", "Work Status")
                },
                allRows,
                filter.Page,
                filter.PageSize,
                new List<string>
                {
                    "Completed work is represented by client/admin approvals and submitted styles.",
                    "Pending work includes styles awaiting admin review and styles not yet submitted."
                });
        }

        private async Task<SectionReportDto> BuildStoresReport(ReportQueryFilter filter)
        {
            var toEnd = ToEndOfDay(filter.DateTo);
            var storeInRecords = await _context.StoreInRecords
                .AsNoTracking()
                .Include(s => s.Cuts)
                    .ThenInclude(c => c.Bundles)
                .Where(s => !string.IsNullOrEmpty(s.CutInDate) && string.Compare(s.CutInDate, filter.DateFrom) >= 0 && string.Compare(s.CutInDate, toEnd) <= 0)
                .ToListAsync();

            var storeInIds = storeInRecords.Select(s => s.Id).ToList();
            var productionRecords = await _context.StoreProductionRecords
                .AsNoTracking()
                .Where(p => storeInIds.Contains(p.StoreInRecordId) || !string.IsNullOrEmpty(p.IssueDate) && string.Compare(p.IssueDate, filter.DateFrom) >= 0 && string.Compare(p.IssueDate, toEnd) <= 0)
                .ToListAsync();

            var logs = await _context.ActivityLogs.AsNoTracking()
                .Where(l => !string.IsNullOrEmpty(l.Timestamp) && string.Compare(l.Timestamp, filter.DateFrom) >= 0 && string.Compare(l.Timestamp, toEnd) <= 0)
                .ToListAsync();

            var productionByStoreIn = productionRecords
                .GroupBy(p => p.StoreInRecordId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var allRows = storeInRecords.Select(s =>
            {
                productionByStoreIn.TryGetValue(s.Id, out var issuedRows);
                issuedRows ??= new List<StoreProductionRecord>();
                var issuedQty = issuedRows.Sum(p => p.IssueQty);
                var status = issuedQty <= 0
                    ? "Not issued"
                    : issuedQty >= s.TotalCutQty && s.TotalCutQty > 0
                        ? "Fully issued"
                        : "Partially issued";
                var actor = LatestActor(logs, s.Id, "StoreIn");

                return Row(new Dictionary<string, object>
                {
                    ["cutInDate"] = s.CutInDate ?? string.Empty,
                    ["styleNo"] = s.StyleNo ?? string.Empty,
                    ["customer"] = s.CustomerName ?? string.Empty,
                    ["scheduleNo"] = s.ScheduleNo ?? string.Empty,
                    ["jobNo"] = s.JobNo ?? string.Empty,
                    ["component"] = s.Components ?? string.Empty,
                    ["bodyColour"] = s.BodyColour ?? string.Empty,
                    ["printColour"] = s.PrintColour ?? string.Empty,
                    ["inAdNo"] = s.InAdNo ?? string.Empty,
                    ["bulkQty"] = s.BulkQty,
                    ["inQty"] = s.InQty,
                    ["cutQty"] = s.TotalCutQty,
                    ["issuedToProduction"] = issuedQty,
                    ["pendingToProduction"] = Math.Max(0, s.TotalCutQty - issuedQty),
                    ["balanceBulkQty"] = s.BalanceBulkQty,
                    ["uncutBalance"] = s.UncutBalance,
                    ["availableQty"] = s.AvailableQty,
                    ["cutCount"] = s.Cuts?.Count ?? 0,
                    ["bundleCount"] = s.Cuts?.Sum(c => c.Bundles?.Count ?? 0) ?? 0,
                    ["status"] = status,
                    ["handledBy"] = actor.UserName,
                    ["lastAction"] = actor.Action
                });
            })
            .Where(r => Match(filter.StyleNo, Convert.ToString(r["styleNo"])))
            .Where(r => Match(filter.Customer, Convert.ToString(r["customer"])))
            .Where(r => Match(filter.ScheduleNo, Convert.ToString(r["scheduleNo"])))
            .Where(r => Match(filter.Status, Convert.ToString(r["status"])))
            .Where(r => string.IsNullOrWhiteSpace(filter.Employee) || Match(filter.Employee, Convert.ToString(r["handledBy"])))
            .OrderByDescending(r => Convert.ToString(r["cutInDate"]))
            .ToList();

            var receivedQty = allRows.Sum(r => ToDouble(r["inQty"]));
            var cutQty = allRows.Sum(r => ToDouble(r["cutQty"]));
            var issuedQtyTotal = allRows.Sum(r => ToDouble(r["issuedToProduction"]));
            var pendingToProduction = allRows.Sum(r => ToDouble(r["pendingToProduction"]));
            var uncutQty = allRows.Sum(r => ToDouble(r["uncutBalance"]));
            var fullyIssued = allRows.Count(r => Convert.ToString(r["status"]) == "Fully issued");
            var partialIssued = allRows.Count(r => Convert.ToString(r["status"]) == "Partially issued");
            var notIssued = allRows.Count(r => Convert.ToString(r["status"]) == "Not issued");

            return SectionReport(
                "Stores",
                "Stores Performance Report",
                "Store-In receiving, cut quantity, issued-to-production quantity, pending quantity and schedule-wise history.",
                new List<MetricCardDto>
                {
                    Metric("Store-In records", allRows.Count, "Received batches"),
                    Metric("Received qty", receivedQty, "Total IN quantity"),
                    Metric("Cut qty", cutQty, "Total cut quantity"),
                    Metric("Issued", issuedQtyTotal, "Moved to production"),
                    Metric("Pending issue", pendingToProduction, "Cut qty not issued"),
                    Metric("Uncut balance", uncutQty, "Received qty not cut")
                },
                new List<ReportChartDto>
                {
                    LineChart("stores-trend", "Stores movement trend", "Daily received quantity compared with production issue quantity.", BuildStoresTrend(storeInRecords, productionRecords, filter.DateFrom, filter.DateTo)),
                    PieChart("stores-status", "Stores issue status", "Store-In records split by production issue status.", new [] { Point("Fully issued", fullyIssued), Point("Partially issued", partialIssued), Point("Not issued", notIssued) }),
                    BarChart("stores-top-styles", "Top styles by received quantity", "Highest received quantity styles in Stores.", allRows.GroupBy(r => Convert.ToString(r["styleNo"]) ?? "").Select(g => Point(g.Key, g.Sum(x => ToDouble(x["inQty"])), Convert.ToString(g.First()["customer"]) ?? "")).OrderByDescending(p => p.Value).Take(10))
                },
                BuildStoreEmployeeRowsFromLogs(logs, allRows),
                new List<ReportColumnDto>
                {
                    Col("cutInDate", "Cut In Date"), Col("styleNo", "Style No"), Col("customer", "Customer"), Col("scheduleNo", "Schedule"),
                    Col("jobNo", "Job No"), Col("component", "Component"), Col("bodyColour", "Body Colour"), Col("inAdNo", "IN-AD"),
                    Col("inQty", "Received", "number"), Col("cutQty", "Cut", "number"), Col("issuedToProduction", "Issued", "number"),
                    Col("pendingToProduction", "Pending", "number"), Col("uncutBalance", "Uncut", "number"), Col("availableQty", "Available", "number"),
                    Col("cutCount", "Cuts", "number"), Col("bundleCount", "Bundles", "number"), Col("status", "Status"), Col("handledBy", "Handled By")
                },
                allRows,
                filter.Page,
                filter.PageSize,
                new List<string>
                {
                    "Pending issue means cut quantity that has not yet moved to production.",
                    "Individual Stores performance uses ActivityLog support because Store-In and Production records do not store CreatedBy directly."
                });
        }

        private async Task<SectionReportDto> BuildQcReport(ReportQueryFilter filter)
        {
            var toEnd = ToEndOfDay(filter.DateTo);
            var reports = await _context.CpiReports
                .AsNoTracking()
                .Where(c => !string.IsNullOrEmpty(c.SummaryDate) && string.Compare(c.SummaryDate, filter.DateFrom) >= 0 && string.Compare(c.SummaryDate, toEnd) <= 0 || !string.IsNullOrEmpty(c.Date) && string.Compare(c.Date, filter.DateFrom) >= 0 && string.Compare(c.Date, toEnd) <= 0)
                .ToListAsync();

            reports = reports
                .Where(c => Match(filter.StyleNo, c.StyleNo))
                .Where(c => Match(filter.Customer, c.Customer))
                .Where(c => Match(filter.ScheduleNo, c.ScheduleNo))
                .Where(c => Match(filter.Status, c.InspectionStatus))
                .Where(c => string.IsNullOrWhiteSpace(filter.Employee) || Match(filter.Employee, c.CheckedBy) || Match(filter.Employee, c.CpiAuditor))
                .ToList();

            var allRows = reports.Select(c =>
            {
                var cutCount = c.CutInspections?.Count ?? 0;
                var defectRows = c.CutInspections?.SelectMany(ci => ci.DefectRows ?? new List<CpiDefectRow>()).ToList() ?? new List<CpiDefectRow>();
                var totalDefected = defectRows.Sum(d => d.DefectedQty);

                return Row(new Dictionary<string, object>
                {
                    ["date"] = c.Date,
                    ["summaryDate"] = c.SummaryDate,
                    ["styleNo"] = c.StyleNo,
                    ["customer"] = c.Customer,
                    ["scheduleNo"] = c.ScheduleNo,
                    ["bodyColour"] = c.BodyColour,
                    ["printColour"] = c.PrintColour,
                    ["receivedQty"] = c.ReceivedQty,
                    ["cpiQty"] = c.CpiQty,
                    ["cuttingQty"] = c.CuttingQty,
                    ["checkedQty"] = c.CheckedQty,
                    ["rejDamageQty"] = c.RejDamageQty,
                    ["balanceQty"] = c.BalanceQty,
                    ["rejectionPercentage"] = c.RejectionPercentage,
                    ["inspectionStatus"] = c.InspectionStatus,
                    ["appRej"] = c.AppRej,
                    ["checkedBy"] = c.CheckedBy,
                    ["cpiAuditor"] = c.CpiAuditor,
                    ["cutInspectionCount"] = cutCount,
                    ["defectedQty"] = Math.Round(totalDefected, 2),
                    ["topDefect"] = TopDefectName(defectRows)
                });
            }).OrderByDescending(r => Convert.ToString(r["summaryDate"])).ToList();

            var passed = reports.Count(r => Same(r.InspectionStatus, "Passed"));
            var failed = reports.Count(r => Same(r.InspectionStatus, "Failed"));
            var pending = reports.Count(r => Same(r.InspectionStatus, "Pending"));
            var checkedQty = reports.Sum(r => r.CheckedQty);
            var rejectedQty = reports.Sum(r => r.RejDamageQty);
            var cpiQty = reports.Sum(r => r.CpiQty);

            return SectionReport(
                "QC",
                "QC / CPI Performance Report",
                "CPI inspection history with checked quantity, passed/failed/pending inspections, defect quantity and auditor performance.",
                new List<MetricCardDto>
                {
                    Metric("CPI reports", reports.Count, "Inspection reports"),
                    Metric("Passed", passed, "Passed inspections"),
                    Metric("Failed", failed, "Failed inspections"),
                    Metric("Pending", pending, "Pending inspections"),
                    Metric("Checked qty", checkedQty, "Pieces checked"),
                    Metric("Rejected/damage", rejectedQty, "Rejected or damaged pieces")
                },
                new List<ReportChartDto>
                {
                    LineChart("qc-trend", "QC checked quantity trend", "Daily checked quantity and rejected/damage quantity.", BuildQcTrend(reports, filter.DateFrom, filter.DateTo)),
                    PieChart("qc-status", "CPI status split", "Passed, failed and pending CPI reports.", new [] { Point("Passed", passed), Point("Failed", failed), Point("Pending", pending) }),
                    BarChart("qc-auditor-output", "Checked quantity by person", "QC output grouped by checked-by name.", reports.GroupBy(r => Clean(r.CheckedBy)).Where(g => g.Key.Length > 0).Select(g => Point(g.Key, g.Sum(x => x.CheckedQty), $"{g.Count()} reports")).OrderByDescending(p => p.Value).Take(10))
                },
                BuildQcEmployeeRows(reports),
                new List<ReportColumnDto>
                {
                    Col("date", "Date"), Col("summaryDate", "Summary Date"), Col("styleNo", "Style No"), Col("customer", "Customer"),
                    Col("scheduleNo", "Schedule"), Col("bodyColour", "Body Colour"), Col("receivedQty", "Received", "number"),
                    Col("cpiQty", "CPI Qty", "number"), Col("checkedQty", "Checked", "number"), Col("rejDamageQty", "Rej/Damage", "number"),
                    Col("balanceQty", "Balance", "number"), Col("rejectionPercentage", "Rejection %"), Col("inspectionStatus", "Status"),
                    Col("checkedBy", "Checked By"), Col("cpiAuditor", "CPI Auditor"), Col("cutInspectionCount", "Cuts", "number"), Col("topDefect", "Top Defect")
                },
                allRows,
                filter.Page,
                filter.PageSize,
                new List<string>
                {
                    "QC quality is displayed using actual pass/fail/pending status and rejected/damage quantity.",
                    "Defect values come from CPI cut-inspection defect rows."
                });
        }

        private async Task<SectionReportDto> BuildGatepassReport(ReportQueryFilter filter)
        {
            var toEnd = ToEndOfDay(filter.DateTo);
            var notes = await _context.AdviceNotes
                .AsNoTracking()
                .Where(a => !string.IsNullOrEmpty(a.DeliveryDate) && string.Compare(a.DeliveryDate, filter.DateFrom) >= 0 && string.Compare(a.DeliveryDate, toEnd) <= 0)
                .ToListAsync();

            notes = notes
                .Where(a => Match(filter.StyleNo, a.StyleNo))
                .Where(a => Match(filter.Customer, a.CustomerName))
                .Where(a => Match(filter.ScheduleNo, a.ScheduleNo))
                .Where(a => string.IsNullOrWhiteSpace(filter.Employee) || Match(filter.Employee, a.PrepByName) || Match(filter.Employee, a.AuthByName) || Match(filter.Employee, a.ReceivedByName))
                .ToList();

            var allRows = notes.Select(a =>
            {
                var noteRows = a.Rows?.Values?.ToList() ?? new List<AdviceNoteRow>();
                var goodQty = noteRows.Sum(r => r.GoodQty);
                var pdQty = noteRows.Sum(r => r.Pd);
                var fdQty = noteRows.Sum(r => r.Fd);
                var status = a.BalanceQty <= 0 ? "Fully dispatched" : "Balance pending";

                return Row(new Dictionary<string, object>
                {
                    ["deliveryDate"] = a.DeliveryDate,
                    ["adNo"] = a.AdNo,
                    ["styleNo"] = a.StyleNo,
                    ["customer"] = a.CustomerName,
                    ["scheduleNo"] = a.ScheduleNo,
                    ["jobNo"] = a.JobNo,
                    ["cutNo"] = a.CutNo,
                    ["component"] = a.Component,
                    ["dispatchQty"] = a.DispatchQty,
                    ["goodQty"] = goodQty,
                    ["pdQty"] = pdQty,
                    ["fdQty"] = fdQty,
                    ["balanceQty"] = a.BalanceQty,
                    ["status"] = status,
                    ["receivedBy"] = a.ReceivedByName,
                    ["preparedBy"] = a.PrepByName,
                    ["authorizedBy"] = a.AuthByName,
                    ["remarks"] = a.Remarks,
                    ["bundleRows"] = noteRows.Count
                });
            })
            .Where(r => Match(filter.Status, Convert.ToString(r["status"])))
            .OrderByDescending(r => Convert.ToString(r["deliveryDate"]))
            .ToList();

            var allNoteRows = notes.SelectMany(a => a.Rows?.Values ?? Enumerable.Empty<AdviceNoteRow>()).ToList();
            var dispatchQty = allRows.Sum(r => ToDouble(r["dispatchQty"]));
            var good = allRows.Sum(r => ToDouble(r["goodQty"]));
            var pd = allRows.Sum(r => ToDouble(r["pdQty"]));
            var fd = allRows.Sum(r => ToDouble(r["fdQty"]));
            var balance = allRows.Sum(r => ToDouble(r["balanceQty"]));

            return SectionReport(
                "Gatepass",
                "Gatepass / Advice Note Performance Report",
                "Advice note dispatch history with dispatched quantity, good quantity, P/D, F/D, balance and responsible names.",
                new List<MetricCardDto>
                {
                    Metric("Advice notes", allRows.Count, "Gatepass documents"),
                    Metric("Dispatch qty", dispatchQty, "Total dispatched pieces"),
                    Metric("Good qty", good, "Good pieces"),
                    Metric("P/D", pd, "Print defects"),
                    Metric("F/D", fd, "Fabric defects"),
                    Metric("Balance qty", balance, "Remaining balance")
                },
                new List<ReportChartDto>
                {
                    LineChart("gatepass-trend", "Dispatch trend", "Daily dispatched and good quantity.", BuildGatepassTrend(notes, filter.DateFrom, filter.DateTo)),
                    PieChart("gatepass-quality", "Dispatch quality split", "Good quantity compared with print and fabric defects.", new [] { Point("Good", good), Point("P/D", pd), Point("F/D", fd) }),
                    BarChart("gatepass-prepared-by", "Dispatch quantity by prepared-by", "Advice note output grouped by prepared-by name.", notes.GroupBy(n => Clean(n.PrepByName)).Where(g => g.Key.Length > 0).Select(g => Point(g.Key, g.Sum(n => n.DispatchQty), $"{g.Count()} notes")).OrderByDescending(p => p.Value).Take(10))
                },
                BuildGatepassEmployeeRows(notes),
                new List<ReportColumnDto>
                {
                    Col("deliveryDate", "Delivery Date"), Col("adNo", "AD No"), Col("styleNo", "Style No"), Col("customer", "Customer"),
                    Col("scheduleNo", "Schedule"), Col("jobNo", "Job No"), Col("cutNo", "Cut No"), Col("component", "Component"),
                    Col("dispatchQty", "Dispatch", "number"), Col("goodQty", "Good", "number"), Col("pdQty", "P/D", "number"),
                    Col("fdQty", "F/D", "number"), Col("balanceQty", "Balance", "number"), Col("status", "Status"),
                    Col("preparedBy", "Prepared By"), Col("authorizedBy", "Authorized By"), Col("receivedBy", "Received By"), Col("remarks", "Remarks")
                },
                allRows,
                filter.Page,
                filter.PageSize,
                new List<string>
                {
                    "P/D and F/D are shown separately so quality issues are visible instead of hidden inside one percentage.",
                    "Prepared, authorized and received names are taken from the advice note footer."
                });
        }

        private async Task<SectionReportDto> BuildWorkerReport(ReportQueryFilter filter)
        {
            var toEnd = ToEndOfDay(filter.DateTo);
            var records = await _context.DailyOutputRecords
                .AsNoTracking()
                .Where(d => !string.IsNullOrEmpty(d.Date) && string.Compare(d.Date, filter.DateFrom) >= 0 && string.Compare(d.Date, toEnd) <= 0 || !string.IsNullOrEmpty(d.CompletedAt) && string.Compare(d.CompletedAt, filter.DateFrom) >= 0 && string.Compare(d.CompletedAt, toEnd) <= 0)
                .ToListAsync();

            var storeInIds = records.Select(r => r.StoreInRecordId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var schedules = await _context.StoreInRecords.AsNoTracking()
                .Where(s => storeInIds.Contains(s.Id))
                .Select(s => new { s.Id, s.ScheduleNo })
                .ToListAsync();
            var scheduleByStoreIn = schedules.ToDictionary(s => s.Id, s => s.ScheduleNo ?? string.Empty);

            records = records
                .Where(d => Match(filter.StyleNo, d.StyleNo))
                .Where(d => Match(filter.Customer, d.CustomerName))
                .Where(d => string.IsNullOrWhiteSpace(filter.ScheduleNo) || (scheduleByStoreIn.TryGetValue(d.StoreInRecordId, out var sch) && Match(filter.ScheduleNo, sch)))
                .Where(d => string.IsNullOrWhiteSpace(filter.Employee) || Match(filter.Employee, d.WorkerName) || Match(filter.Employee, d.CompletedBy))
                .Where(d => string.IsNullOrWhiteSpace(filter.Status) || Match(filter.Status, d.IsJobCompleted ? "Completed" : WorkerPendingStatus(d)))
                .Where(d => string.IsNullOrWhiteSpace(filter.TimeSlot) || HasTimeSlotWork(d, filter.TimeSlot))
                .ToList();

            var allRows = records.Select(d => Row(new Dictionary<string, object>
            {
                ["date"] = d.Date,
                ["styleNo"] = d.StyleNo,
                ["customer"] = d.CustomerName,
                ["scheduleNo"] = scheduleByStoreIn.TryGetValue(d.StoreInRecordId, out var sch) ? sch : string.Empty,
                ["cutNo"] = d.CutNo,
                ["component"] = d.Component,
                ["orderQty"] = d.OrderQty,
                ["tableNo"] = d.TableNo,
                ["workerName"] = d.WorkerName,
                ["seating"] = d.TotalSeating,
                ["printing"] = d.TotalPrinting,
                ["curing"] = d.TotalCuring,
                ["checking"] = d.TotalChecking,
                ["packing"] = d.TotalPacking,
                ["dispatch"] = d.TotalDispatch,
                ["pendingQty"] = Math.Max(0, d.OrderQty - new[] { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch }.Max()),
                ["status"] = d.IsJobCompleted ? "Completed" : WorkerPendingStatus(d),
                ["completedAt"] = d.CompletedAt,
                ["completedBy"] = d.CompletedBy,
                ["timeSlotCount"] = d.TimeSlots?.Count(t => t.Seating + t.Printing + t.Curing + t.Checking + t.Packing + t.Dispatch > 0) ?? 0
            })).OrderByDescending(r => Convert.ToString(r["date"])).ToList();

            var normalRecords = records.Where(r => !r.IsJobCompleted).ToList();
            var completed = records.Count(r => r.IsJobCompleted);
            var pending = normalRecords.Count(r => WorkerPendingStatus(r) != "Fully allocated");

            return SectionReport(
                "Worker",
                "Worker Performance Report",
                "Daily output history by worker, table, style, cut, component and production stage. Stages are reported separately.",
                new List<MetricCardDto>
                {
                    Metric("Output records", normalRecords.Count, "Daily output rows"),
                    Metric("Workers", normalRecords.Select(r => Clean(r.WorkerName)).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Unique worker names"),
                    Metric("Seating", normalRecords.Sum(r => r.TotalSeating), "Stage quantity"),
                    Metric("Printing", normalRecords.Sum(r => r.TotalPrinting), "Stage quantity"),
                    Metric("Dispatch", normalRecords.Sum(r => r.TotalDispatch), "Stage quantity"),
                    Metric("Pending jobs", pending, "Not fully allocated")
                },
                new List<ReportChartDto>
                {
                    LineChart("worker-trend", "Worker output trend", "Daily highest stage output and dispatch stage output.", BuildWorkerTrend(normalRecords, filter.DateFrom, filter.DateTo)),
                    BarChart("worker-stage-output", "Stage output", "Seating, printing, curing, checking, packing and dispatch quantities.", new []
                    {
                        Point("Seating", normalRecords.Sum(r => r.TotalSeating)),
                        Point("Printing", normalRecords.Sum(r => r.TotalPrinting)),
                        Point("Curing", normalRecords.Sum(r => r.TotalCuring)),
                        Point("Checking", normalRecords.Sum(r => r.TotalChecking)),
                        Point("Packing", normalRecords.Sum(r => r.TotalPacking)),
                        Point("Dispatch", normalRecords.Sum(r => r.TotalDispatch))
                    }),
                    PieChart("worker-status", "Worker work status", "Fully allocated, pending and manually completed job markers.", new [] { Point("Fully allocated", normalRecords.Count(r => WorkerPendingStatus(r) == "Fully allocated")), Point("Pending", pending), Point("Completed markers", completed) })
                },
                BuildWorkerEmployeeRows(records),
                new List<ReportColumnDto>
                {
                    Col("date", "Date"), Col("styleNo", "Style No"), Col("customer", "Customer"), Col("scheduleNo", "Schedule"), Col("cutNo", "Cut No"),
                    Col("component", "Component"), Col("orderQty", "Order Qty", "number"), Col("tableNo", "Table"), Col("workerName", "Worker"),
                    Col("seating", "Seating", "number"), Col("printing", "Printing", "number"), Col("curing", "Curing", "number"),
                    Col("checking", "Checking", "number"), Col("packing", "Packing", "number"), Col("dispatch", "Dispatch", "number"),
                    Col("pendingQty", "Pending", "number"), Col("status", "Status"), Col("completedBy", "Completed By")
                },
                allRows,
                filter.Page,
                filter.PageSize,
                new List<string>
                {
                    "Worker stages are independent. The report does not add all stages together as finished garments.",
                    "For wage support, output uses the highest stage quantity per record to reduce double-counting."
                });
        }

        private async Task<ReportSnapshot> LoadSnapshot(string from, string to)
        {
            var toEnd = ToEndOfDay(to);

            var sampleStyles = await _context.SampleStyles.AsNoTracking()
                .Where(s => !string.IsNullOrEmpty(s.CreatedAt) && string.Compare(s.CreatedAt, from) >= 0 && string.Compare(s.CreatedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.UpdatedAt) && string.Compare(s.UpdatedAt, from) >= 0 && string.Compare(s.UpdatedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.SubmittedAt) && string.Compare(s.SubmittedAt, from) >= 0 && string.Compare(s.SubmittedAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.AdminActionAt) && string.Compare(s.AdminActionAt, from) >= 0 && string.Compare(s.AdminActionAt, toEnd) <= 0 || !string.IsNullOrEmpty(s.ClientApprovedAt) && string.Compare(s.ClientApprovedAt, from) >= 0 && string.Compare(s.ClientApprovedAt, toEnd) <= 0)
                .ToListAsync();

            var storeInRecords = await _context.StoreInRecords.AsNoTracking()
                .Include(s => s.Cuts).ThenInclude(c => c.Bundles)
                .Where(s => !string.IsNullOrEmpty(s.CutInDate) && string.Compare(s.CutInDate, from) >= 0 && string.Compare(s.CutInDate, toEnd) <= 0)
                .ToListAsync();

            var productionRecords = await _context.StoreProductionRecords.AsNoTracking()
                .Where(p => !string.IsNullOrEmpty(p.IssueDate) && string.Compare(p.IssueDate, from) >= 0 && string.Compare(p.IssueDate, toEnd) <= 0)
                .ToListAsync();

            var cpiReports = await _context.CpiReports.AsNoTracking()
                .Where(c => !string.IsNullOrEmpty(c.SummaryDate) && string.Compare(c.SummaryDate, from) >= 0 && string.Compare(c.SummaryDate, toEnd) <= 0 || !string.IsNullOrEmpty(c.Date) && string.Compare(c.Date, from) >= 0 && string.Compare(c.Date, toEnd) <= 0)
                .ToListAsync();

            var adviceNotes = await _context.AdviceNotes.AsNoTracking()
                .Where(a => !string.IsNullOrEmpty(a.DeliveryDate) && string.Compare(a.DeliveryDate, from) >= 0 && string.Compare(a.DeliveryDate, toEnd) <= 0)
                .ToListAsync();

            var dailyOutputs = await _context.DailyOutputRecords.AsNoTracking()
                .Where(d => !string.IsNullOrEmpty(d.Date) && string.Compare(d.Date, from) >= 0 && string.Compare(d.Date, toEnd) <= 0 || !string.IsNullOrEmpty(d.CompletedAt) && string.Compare(d.CompletedAt, from) >= 0 && string.Compare(d.CompletedAt, toEnd) <= 0)
                .ToListAsync();

            var activityLogs = await _context.ActivityLogs.AsNoTracking()
                .Where(a => !string.IsNullOrEmpty(a.Timestamp) && string.Compare(a.Timestamp, from) >= 0 && string.Compare(a.Timestamp, toEnd) <= 0)
                .ToListAsync();

            return new ReportSnapshot
            {
                SampleStyles = sampleStyles,
                StoreInRecords = storeInRecords,
                ProductionRecords = productionRecords,
                CpiReports = cpiReports,
                AdviceNotes = adviceNotes,
                DailyOutputs = dailyOutputs,
                ActivityLogs = activityLogs
            };
        }

        private static List<DepartmentSummaryDto> BuildDepartmentCards(ReportSnapshot snapshot)
        {
            var rows = new List<DepartmentSummaryDto>();

            var styles = snapshot.SampleStyles;
            var stylesApproved = styles.Count(s => Same(s.AdminStatus, "Approved"));
            var stylesRejected = styles.Count(s => Same(s.AdminStatus, "Rejected"));
            var stylesSubmitted = styles.Count(s => s.SubmittedToAdmin || !string.IsNullOrWhiteSpace(s.SubmittedAt));
            var stylesPending = styles.Count - stylesApproved;
            rows.Add(new DepartmentSummaryDto
            {
                Section = "Development",
                Title = "Development style movement",
                PrimaryMetric = $"{styles.Count} samples/styles",
                SecondaryMetric = $"{stylesSubmitted} submitted · {stylesApproved} approved · {stylesRejected} rejected",
                CompletedLabel = "Admin approved",
                CompletedValue = stylesApproved,
                PendingLabel = "Pending / not submitted",
                PendingValue = stylesPending,
                WorkloadValue = styles.Count
            });

            var receivedQty = snapshot.StoreInRecords.Sum(s => s.InQty);
            var issuedQty = snapshot.ProductionRecords.Sum(p => p.IssueQty);
            var storesPending = Math.Max(0, snapshot.StoreInRecords.Sum(s => s.TotalCutQty) - issuedQty) + snapshot.StoreInRecords.Sum(s => s.UncutBalance);
            rows.Add(new DepartmentSummaryDto
            {
                Section = "Stores",
                Title = "Stores receiving and issue",
                PrimaryMetric = $"{receivedQty} received",
                SecondaryMetric = $"{issuedQty} issued · {storesPending} pending/uncut",
                CompletedLabel = "Issued to production",
                CompletedValue = issuedQty,
                PendingLabel = "Pending / uncut",
                PendingValue = storesPending,
                WorkloadValue = receivedQty
            });

            var cpiPassed = snapshot.CpiReports.Count(c => Same(c.InspectionStatus, "Passed"));
            var cpiFailed = snapshot.CpiReports.Count(c => Same(c.InspectionStatus, "Failed"));
            var cpiPending = snapshot.CpiReports.Count(c => Same(c.InspectionStatus, "Pending"));
            rows.Add(new DepartmentSummaryDto
            {
                Section = "QC",
                Title = "QC / CPI inspection",
                PrimaryMetric = $"{snapshot.CpiReports.Sum(c => c.CheckedQty)} checked",
                SecondaryMetric = $"{cpiPassed} passed · {cpiFailed} failed · {cpiPending} pending",
                CompletedLabel = "Passed / failed checked",
                CompletedValue = cpiPassed + cpiFailed,
                PendingLabel = "Pending inspections",
                PendingValue = cpiPending,
                WorkloadValue = snapshot.CpiReports.Sum(c => c.CheckedQty)
            });

            var gateRows = snapshot.AdviceNotes.SelectMany(a => a.Rows?.Values ?? Enumerable.Empty<AdviceNoteRow>()).ToList();
            var dispatchQty = snapshot.AdviceNotes.Sum(a => a.DispatchQty);
            var gateGood = gateRows.Sum(r => r.GoodQty);
            var gateDefect = gateRows.Sum(r => r.Pd + r.Fd);
            var gateBalance = snapshot.AdviceNotes.Sum(a => a.BalanceQty);
            rows.Add(new DepartmentSummaryDto
            {
                Section = "Gatepass",
                Title = "Gatepass dispatch",
                PrimaryMetric = $"{dispatchQty} dispatched",
                SecondaryMetric = $"{gateGood} good · {gateDefect} defects · {gateBalance} balance",
                CompletedLabel = "Dispatched",
                CompletedValue = dispatchQty,
                PendingLabel = "Balance",
                PendingValue = gateBalance,
                WorkloadValue = dispatchQty
            });

            var workerRows = snapshot.DailyOutputs.Where(d => !d.IsJobCompleted).ToList();
            var workerOutput = workerRows.Sum(StageOutputForPerformance);
            var workerPending = workerRows.Sum(d => Math.Max(0, d.OrderQty - new[] { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch }.Max()));
            rows.Add(new DepartmentSummaryDto
            {
                Section = "Worker",
                Title = "Worker daily output",
                PrimaryMetric = $"{workerRows.Count} output records",
                SecondaryMetric = $"{workerOutput} stage output · {workerPending} pending qty",
                CompletedLabel = "Stage output",
                CompletedValue = workerOutput,
                PendingLabel = "Pending qty",
                PendingValue = workerPending,
                WorkloadValue = workerOutput
            });

            var maxWorkload = Math.Max(1, rows.Max(r => r.WorkloadValue));
            foreach (var row in rows)
            {
                row.CompletionPercent = Percent(row.CompletedValue, row.CompletedValue + row.PendingValue);
                row.WorkloadSharePercent = Percent(row.WorkloadValue, maxWorkload);
                row.PerformanceScore = WeightedScore(row.CompletionPercent, Math.Max(0, 100 - Percent(row.PendingValue, row.CompletedValue + row.PendingValue)), row.WorkloadSharePercent);
            }

            return rows;
        }

        private static List<MetricCardDto> BuildHeadlineCards(ReportSnapshot snapshot)
        {
            return new List<MetricCardDto>
            {
                Metric("Styles created", snapshot.SampleStyles.Count, "Development records"),
                Metric("Store received", snapshot.StoreInRecords.Sum(s => s.InQty), "Pieces received"),
                Metric("QC checked", snapshot.CpiReports.Sum(c => c.CheckedQty), "Pieces inspected"),
                Metric("Dispatched", snapshot.AdviceNotes.Sum(a => a.DispatchQty), "Gatepass output"),
                Metric("Worker output", snapshot.DailyOutputs.Where(d => !d.IsJobCompleted).Sum(StageOutputForPerformance), "Highest stage qty per row"),
                Metric("Active users", snapshot.ActivityLogs.Select(a => a.UserName).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Users/operators with activity")
            };
        }

        private static List<EmployeePerformanceDto> BuildEmployeePerformance(ReportSnapshot snapshot)
        {
            var rows = new List<EmployeePerformanceDto>();
            rows.AddRange(BuildDevelopmentEmployeeRows(snapshot.SampleStyles));
            rows.AddRange(BuildQcEmployeeRows(snapshot.CpiReports));
            rows.AddRange(BuildGatepassEmployeeRows(snapshot.AdviceNotes));
            rows.AddRange(BuildWorkerEmployeeRows(snapshot.DailyOutputs));
            rows.AddRange(BuildStoreEmployeeRowsFromLogs(snapshot.ActivityLogs, new List<Dictionary<string, object>>()));

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.EmployeeName))
                .GroupBy(r => new { Name = r.EmployeeName.Trim(), Dept = r.Department })
                .Select(g => new EmployeePerformanceDto
                {
                    EmployeeName = g.Key.Name,
                    Department = g.Key.Dept,
                    Role = string.Join(", ", g.Select(x => x.Role).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                    RecordCount = g.Sum(x => x.RecordCount),
                    OutputQty = g.Sum(x => x.OutputQty),
                    CompletedQty = g.Sum(x => x.CompletedQty),
                    PendingQty = g.Sum(x => x.PendingQty),
                    QualityQty = g.Sum(x => x.QualityQty),
                    DefectQty = g.Sum(x => x.DefectQty),
                    Score = Math.Round(g.Average(x => x.Score), 2),
                    Basis = string.Join(" | ", g.Select(x => x.Basis).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(3))
                })
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.OutputQty)
                .ToList();
        }

        private static List<EmployeePerformanceDto> BuildDevelopmentEmployeeRows(List<SampleStyle> styles)
        {
            var rows = new List<EmployeePerformanceDto>();

            rows.AddRange(styles
                .SelectMany(s => s.Revisions ?? new List<SampleStyleRevision>())
                .Where(r => !string.IsNullOrWhiteSpace(r.CreatedBy))
                .GroupBy(r => r.CreatedBy.Trim())
                .Select(g => new EmployeePerformanceDto
                {
                    EmployeeName = g.Key,
                    Role = "Developer",
                    Department = "Development",
                    RecordCount = g.Count(),
                    OutputQty = g.Count(),
                    CompletedQty = g.Count(),
                    PendingQty = 0,
                    QualityQty = g.Count(),
                    DefectQty = 0,
                    Score = 100,
                    Basis = "Revision/correction entries completed"
                }));

            rows.AddRange(styles
                .Where(s => !string.IsNullOrWhiteSpace(s.AdminActionBy) || !string.IsNullOrWhiteSpace(s.ClientApprovedBy))
                .SelectMany(s => new[] { s.AdminActionBy, s.ClientApprovedBy }.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => new { Person = v!.Trim(), Style = s }))
                .GroupBy(x => x.Person)
                .Select(g => new EmployeePerformanceDto
                {
                    EmployeeName = g.Key,
                    Role = "Developer/Admin",
                    Department = "Development",
                    RecordCount = g.Count(),
                    OutputQty = g.Count(),
                    CompletedQty = g.Count(x => Same(x.Style.AdminStatus, "Approved") || x.Style.ClientApproved),
                    PendingQty = g.Count(x => !Same(x.Style.AdminStatus, "Approved")),
                    QualityQty = g.Count(x => Same(x.Style.AdminStatus, "Approved")),
                    DefectQty = g.Count(x => Same(x.Style.AdminStatus, "Rejected")),
                    Score = WeightedScore(Percent(g.Count(x => x.Style.SubmittedToAdmin), g.Count()), Percent(g.Count(x => Same(x.Style.AdminStatus, "Approved")), g.Count(x => Same(x.Style.AdminStatus, "Approved") || Same(x.Style.AdminStatus, "Rejected"))), 100),
                    Basis = "Client/admin approval responsibility"
                }));

            return rows;
        }

        private static List<EmployeePerformanceDto> BuildStoreEmployeeRowsFromLogs(List<ActivityLog> logs, List<Dictionary<string, object>> storeRows)
        {
            var relevant = logs
                .Where(l => Same(l.UserRole, "Stores") || Same(l.Entity, "StoreIn") || Same(l.Entity, "Production") || Same(l.Entity, "StoreProduction"))
                .Where(l => !string.IsNullOrWhiteSpace(l.UserName))
                .ToList();

            return relevant.GroupBy(l => Clean(l.UserName)).Select(g => new EmployeePerformanceDto
            {
                EmployeeName = g.Key,
                Role = "Stores",
                Department = "Stores",
                RecordCount = g.Count(),
                OutputQty = g.Count(),
                CompletedQty = g.Count(l => l.Action == "Create" || l.Action == "Update"),
                PendingQty = 0,
                QualityQty = g.Count(l => l.Action != "Delete"),
                DefectQty = g.Count(l => l.Action == "Delete"),
                Score = WeightedScore(Percent(g.Count(l => l.Action != "Delete"), g.Count()), 100, Percent(g.Count(), Math.Max(1, relevant.Count))),
                Basis = "Stores activity log support. Store-In/Production models do not store CreatedBy directly."
            }).ToList();
        }

        private static List<EmployeePerformanceDto> BuildQcEmployeeRows(List<CPIReport> reports)
        {
            var rows = new List<EmployeePerformanceDto>();
            rows.AddRange(reports
                .Where(r => !string.IsNullOrWhiteSpace(r.CheckedBy))
                .GroupBy(r => r.CheckedBy.Trim())
                .Select(g => new EmployeePerformanceDto
                {
                    EmployeeName = g.Key,
                    Role = "QC",
                    Department = "QC",
                    RecordCount = g.Count(),
                    OutputQty = g.Sum(r => r.CheckedQty),
                    CompletedQty = g.Count(r => !Same(r.InspectionStatus, "Pending")),
                    PendingQty = g.Count(r => Same(r.InspectionStatus, "Pending")),
                    QualityQty = g.Count(r => Same(r.InspectionStatus, "Passed")),
                    DefectQty = g.Sum(r => r.RejDamageQty),
                    Score = WeightedScore(Percent(g.Count(r => !Same(r.InspectionStatus, "Pending")), g.Count()), Percent(g.Count(r => Same(r.InspectionStatus, "Passed")), g.Count(r => Same(r.InspectionStatus, "Passed") || Same(r.InspectionStatus, "Failed"))), Percent(g.Sum(r => r.CheckedQty), Math.Max(1, reports.Max(r => r.CheckedQty)))) ,
                    Basis = "Checked quantity and CPI status"
                }));

            rows.AddRange(reports
                .Where(r => !string.IsNullOrWhiteSpace(r.CpiAuditor))
                .GroupBy(r => r.CpiAuditor.Trim())
                .Select(g => new EmployeePerformanceDto
                {
                    EmployeeName = g.Key,
                    Role = "QC Auditor",
                    Department = "QC",
                    RecordCount = g.Count(),
                    OutputQty = g.Sum(r => r.CpiQty),
                    CompletedQty = g.Count(r => !Same(r.InspectionStatus, "Pending")),
                    PendingQty = g.Count(r => Same(r.InspectionStatus, "Pending")),
                    QualityQty = g.Count(r => Same(r.InspectionStatus, "Passed")),
                    DefectQty = g.Sum(r => r.RejDamageQty),
                    Score = WeightedScore(Percent(g.Count(r => !Same(r.InspectionStatus, "Pending")), g.Count()), Percent(g.Count(r => Same(r.InspectionStatus, "Passed")), g.Count(r => Same(r.InspectionStatus, "Passed") || Same(r.InspectionStatus, "Failed"))), 100),
                    Basis = "CPI auditor report count and outcome"
                }));

            return rows;
        }

        private static List<EmployeePerformanceDto> BuildGatepassEmployeeRows(List<AdviceNoteRecord> notes)
        {
            var rows = new List<EmployeePerformanceDto>();
            rows.AddRange(BuildAdvicePersonRows(notes, "Prepared By", n => n.PrepByName));
            rows.AddRange(BuildAdvicePersonRows(notes, "Authorized By", n => n.AuthByName));
            rows.AddRange(BuildAdvicePersonRows(notes, "Received By", n => n.ReceivedByName));
            return rows;
        }

        private static IEnumerable<EmployeePerformanceDto> BuildAdvicePersonRows(List<AdviceNoteRecord> notes, string role, Func<AdviceNoteRecord, string> selector)
        {
            var maxDispatch = notes.GroupBy(selector).Select(g => g.Sum(n => n.DispatchQty)).DefaultIfEmpty(0).Max();
            return notes
                .Where(n => !string.IsNullOrWhiteSpace(selector(n)))
                .GroupBy(n => selector(n).Trim())
                .Select(g =>
                {
                    var noteRows = g.SelectMany(n => n.Rows?.Values ?? Enumerable.Empty<AdviceNoteRow>()).ToList();
                    var total = noteRows.Sum(r => r.TotalPcs);
                    var good = noteRows.Sum(r => r.GoodQty);
                    var defects = noteRows.Sum(r => r.Pd + r.Fd);
                    return new EmployeePerformanceDto
                    {
                        EmployeeName = g.Key,
                        Role = role,
                        Department = "Gatepass",
                        RecordCount = g.Count(),
                        OutputQty = g.Sum(n => n.DispatchQty),
                        CompletedQty = g.Sum(n => n.DispatchQty),
                        PendingQty = g.Sum(n => n.BalanceQty),
                        QualityQty = good,
                        DefectQty = defects,
                        Score = WeightedScore(Percent(g.Sum(n => n.DispatchQty), Math.Max(1, maxDispatch)), Percent(good, total), Percent(g.Count(), Math.Max(1, notes.Count))),
                        Basis = "Advice note dispatch quantity and footer responsibility"
                    };
                });
        }

        private static List<EmployeePerformanceDto> BuildWorkerEmployeeRows(List<DailyOutputRecord> records)
        {
            var normal = records.Where(r => !r.IsJobCompleted).ToList();
            var maxOutput = normal.GroupBy(r => Clean(r.WorkerName)).Select(g => g.Sum(StageOutputForPerformance)).DefaultIfEmpty(0).Max();

            return normal
                .Where(r => !string.IsNullOrWhiteSpace(r.WorkerName))
                .GroupBy(r => r.WorkerName.Trim())
                .Select(g =>
                {
                    var output = g.Sum(StageOutputForPerformance);
                    var completedMarkers = records.Count(r => r.IsJobCompleted && Same(Clean(r.CompletedBy), g.Key));
                    var pendingQty = g.Sum(r => Math.Max(0, r.OrderQty - new[] { r.TotalSeating, r.TotalPrinting, r.TotalCuring, r.TotalChecking, r.TotalPacking, r.TotalDispatch }.Max()));
                    return new EmployeePerformanceDto
                    {
                        EmployeeName = g.Key,
                        Role = "Worker",
                        Department = "Worker",
                        RecordCount = g.Count(),
                        OutputQty = output,
                        CompletedQty = completedMarkers + g.Count(r => WorkerPendingStatus(r) == "Fully allocated"),
                        PendingQty = pendingQty,
                        QualityQty = g.Sum(r => r.TotalChecking + r.TotalPacking + r.TotalDispatch),
                        DefectQty = 0,
                        Score = WeightedScore(Percent(output, maxOutput), 100, Percent(g.Count(r => WorkerPendingStatus(r) == "Fully allocated") + completedMarkers, Math.Max(1, g.Count() + completedMarkers))),
                        Basis = "Stage-wise output. Highest stage quantity is used to avoid double-counting."
                    };
                }).ToList();
        }

        private static int StageOutputForPerformance(DailyOutputRecord r)
        {
            return new[] { r.TotalSeating, r.TotalPrinting, r.TotalCuring, r.TotalChecking, r.TotalPacking, r.TotalDispatch }.Max();
        }

        private static void ApplyReportEmployeeFilters(SectionReportDto report, ReportQueryFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Employee))
            {
                report.EmployeeRows = report.EmployeeRows
                    .Where(e => Match(filter.Employee, e.EmployeeName))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                report.EmployeeRows = report.EmployeeRows
                    .Where(e => e.Role.Contains(filter.Role, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private SectionReportDto SectionReport(
            string section,
            string title,
            string description,
            List<MetricCardDto> metrics,
            List<ReportChartDto> charts,
            List<EmployeePerformanceDto> employeeRows,
            List<ReportColumnDto> columns,
            List<Dictionary<string, object>> rows,
            int page,
            int pageSize,
            List<string>? notes = null)
        {
            var total = rows.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(Math.Max(1, page), totalPages);

            return new SectionReportDto
            {
                Section = section,
                Title = title,
                Description = description,
                Metrics = metrics,
                Charts = charts,
                EmployeeRows = employeeRows.OrderByDescending(e => e.Score).ThenByDescending(e => e.OutputQty).ToList(),
                Columns = columns,
                Rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalRows = total,
                TotalPages = totalPages,
                Notes = notes ?? new List<string>()
            };
        }

        private static List<ChartPointDto> BuildDevelopmentTrend(List<SampleStyle> styles, string from, string to)
        {
            return DateLabels(from, to).Select(date => Point(date,
                styles.Count(s => BucketDate(s.CreatedAt, from, to) == date),
                styles.Count(s => BucketDate(s.SubmittedAt, from, to) == date),
                styles.Count(s => BucketDate(s.AdminActionAt, from, to) == date && Same(s.AdminStatus, "Approved")),
                "Created / Submitted / Approved"
            )).ToList();
        }

        private static List<ChartPointDto> BuildStoresTrend(List<StoreInRecord> storeIn, List<StoreProductionRecord> production, string from, string to)
        {
            return DateLabels(from, to).Select(date => Point(date,
                storeIn.Where(s => BucketDate(s.CutInDate, from, to) == date).Sum(s => s.InQty),
                production.Where(p => BucketDate(p.IssueDate, from, to) == date).Sum(p => p.IssueQty),
                0,
                "Received / Issued"
            )).ToList();
        }

        private static List<ChartPointDto> BuildQcTrend(List<CPIReport> reports, string from, string to)
        {
            return DateLabels(from, to).Select(date => Point(date,
                reports.Where(r => BucketDate(r.SummaryDate, from, to) == date || BucketDate(r.Date, from, to) == date).Sum(r => r.CheckedQty),
                reports.Where(r => BucketDate(r.SummaryDate, from, to) == date || BucketDate(r.Date, from, to) == date).Sum(r => r.RejDamageQty),
                0,
                "Checked / Rejected"
            )).ToList();
        }

        private static List<ChartPointDto> BuildGatepassTrend(List<AdviceNoteRecord> notes, string from, string to)
        {
            return DateLabels(from, to).Select(date => Point(date,
                notes.Where(n => BucketDate(n.DeliveryDate, from, to) == date).Sum(n => n.DispatchQty),
                notes.Where(n => BucketDate(n.DeliveryDate, from, to) == date).SelectMany(n => n.Rows?.Values ?? Enumerable.Empty<AdviceNoteRow>()).Sum(r => r.GoodQty),
                0,
                "Dispatched / Good"
            )).ToList();
        }

        private static List<ChartPointDto> BuildWorkerTrend(List<DailyOutputRecord> records, string from, string to)
        {
            return DateLabels(from, to).Select(date => Point(date,
                records.Where(r => BucketDate(r.Date, from, to) == date).Sum(StageOutputForPerformance),
                records.Where(r => BucketDate(r.Date, from, to) == date).Sum(r => r.TotalDispatch),
                0,
                "Output / Dispatch stage"
            )).ToList();
        }

        private static List<ChartPointDto> BuildOverviewTrend(ReportSnapshot snapshot, string from, string to)
        {
            return DateLabels(from, to).Select(date => Point(date,
                snapshot.SampleStyles.Count(s => BucketDate(s.CreatedAt, from, to) == date),
                snapshot.StoreInRecords.Where(s => BucketDate(s.CutInDate, from, to) == date).Sum(s => s.InQty),
                snapshot.AdviceNotes.Where(a => BucketDate(a.DeliveryDate, from, to) == date).Sum(a => a.DispatchQty),
                "Styles / Store received / Dispatched"
            )).ToList();
        }

        private static List<string> DateLabels(string from, string to)
        {
            if (!DateTime.TryParse(from, out var start) || !DateTime.TryParse(to, out var end))
                return new List<string>();
            if (end < start) return new List<string>();
            var days = (end.Date - start.Date).Days;
            if (days > 62)
            {
                return Enumerable.Range(0, days + 1)
                    .Select(i => start.AddDays(i))
                    .GroupBy(d => new DateTime(d.Year, d.Month, 1))
                    .Select(g => g.Key.ToString("yyyy-MM-dd"))
                    .ToList();
            }
            return Enumerable.Range(0, days + 1).Select(i => start.AddDays(i).ToString("yyyy-MM-dd")).ToList();
        }

        private async Task<List<string>> LoadWorkerSchedules(List<string> storeInIds)
        {
            if (!storeInIds.Any()) return new List<string>();
            var rows = await _context.StoreInRecords.AsNoTracking()
                .Where(s => storeInIds.Contains(s.Id))
                .Select(s => s.ScheduleNo)
                .ToListAsync();
            return Distinct(rows);
        }

        private static bool HasTimeSlotWork(DailyOutputRecord record, string timeSlot)
        {
            if (string.IsNullOrWhiteSpace(timeSlot)) return true;
            return record.TimeSlots?.Any(t => $"{t.TimeFrom}-{t.TimeTo}" == timeSlot && (t.Seating + t.Printing + t.Curing + t.Checking + t.Packing + t.Dispatch) > 0) ?? false;
        }

        private static (string UserName, string Action) LatestActor(List<ActivityLog> logs, string entityId, string entity)
        {
            var log = logs
                .Where(l => l.EntityId == entityId && (Same(l.Entity, entity) || Same(l.Entity, "Production") || Same(l.Entity, "StoreProduction")))
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefault();
            return (log?.UserName ?? string.Empty, log?.Action ?? string.Empty);
        }

        private static IEnumerable<string> DevelopmentPeople(SampleStyle s)
        {
            var list = new List<string?> { s.ClientApprovedBy, s.AdminActionBy };
            if (s.Revisions != null) list.AddRange(s.Revisions.Select(r => r.CreatedBy));
            return list.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim());
        }

        private static string ResolveDevelopmentStatus(SampleStyle style)
        {
            if (Same(style.AdminStatus, "Approved")) return "Admin approved";
            if (Same(style.AdminStatus, "Rejected")) return "Admin rejected";
            if (style.SubmittedToAdmin || !string.IsNullOrWhiteSpace(style.SubmittedAt)) return "Awaiting admin";
            if (style.ClientApproved) return "Client approved, not submitted";
            return "Sample created";
        }

        private static string WorkerPendingStatus(DailyOutputRecord r)
        {
            if (r.IsJobCompleted) return "Completed";
            var maxStage = new[] { r.TotalSeating, r.TotalPrinting, r.TotalCuring, r.TotalChecking, r.TotalPacking, r.TotalDispatch }.Max();
            return r.OrderQty > 0 && maxStage >= r.OrderQty ? "Fully allocated" : "Pending";
        }

        private static string TopDefectName(List<CpiDefectRow> rows)
        {
            return rows
                .Where(r => r.DefectedQty > 0)
                .GroupBy(r => string.IsNullOrWhiteSpace(r.DefectName) ? r.DefectCode : r.DefectName)
                .OrderByDescending(g => g.Sum(x => x.DefectedQty))
                .Select(g => $"{g.Key} ({Math.Round(g.Sum(x => x.DefectedQty), 2)})")
                .FirstOrDefault() ?? string.Empty;
        }

        private static ReportColumnDto Col(string key, string label, string type = "text") => new() { Key = key, Label = label, Type = type };
        private static MetricCardDto Metric(string label, double value, string note = "") => new() { Label = label, Value = Math.Round(value, 2), Note = note };
        private static ReportChartDto BarChart(string key, string title, string description, IEnumerable<ChartPointDto> points) => new() { Key = key, Type = "bar", Title = title, Description = description, Points = points.Where(p => p.Value > 0 || p.Value2 > 0 || p.Value3 > 0).ToList() };
        private static ReportChartDto PieChart(string key, string title, string description, IEnumerable<ChartPointDto> points) => new() { Key = key, Type = "pie", Title = title, Description = description, Points = points.Where(p => p.Value > 0).ToList() };
        private static ReportChartDto LineChart(string key, string title, string description, IEnumerable<ChartPointDto> points) => new() { Key = key, Type = "line", Title = title, Description = description, Points = points.ToList() };
        private static ChartPointDto Point(string label, double value, string detail = "") => new() { Label = label, Value = Math.Round(value, 2), Detail = detail };
        private static ChartPointDto Point(string label, double value, double value2, double value3, string detail = "") => new() { Label = label, Value = Math.Round(value, 2), Value2 = Math.Round(value2, 2), Value3 = Math.Round(value3, 2), Detail = detail };
        private static Dictionary<string, object> Row(Dictionary<string, object> values) => values;

        private static (string From, string To) ResolveDateRange(string? dateFrom, string? dateTo)
        {
            var today = DateTime.UtcNow.AddHours(5.5).Date;
            var from = string.IsNullOrWhiteSpace(dateFrom) ? new DateTime(today.Year, today.Month, 1).ToString("yyyy-MM-dd") : dateFrom.Trim();
            var to = string.IsNullOrWhiteSpace(dateTo) ? today.ToString("yyyy-MM-dd") : dateTo.Trim();
            return (from, to);
        }

        private static string NowStamp() => DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM-dd HH:mm:ss");
        private static string ToEndOfDay(string date) => date.Length <= 10 ? date + " 23:59:59" : date;
        private static bool Same(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
        private static string Clean(string? value) => value?.Trim() ?? string.Empty;
        private static string YesNo(bool value) => value ? "Yes" : "No";
        private static int ParseInt(string? value) => int.TryParse(value, out var n) ? n : 0;
        private static double ToDouble(object? value) => double.TryParse(Convert.ToString(value), out var n) ? n : 0;
        private static string DatePart(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Length >= 10 ? value.Trim()[..10] : value.Trim();
        private static string BucketDate(string? value, string from, string to)
        {
            var part = DatePart(value);
            if (string.IsNullOrWhiteSpace(part)) return string.Empty;
            return UseMonthlyBuckets(from, to) && part.Length >= 7 ? part[..7] + "-01" : part;
        }
        private static bool UseMonthlyBuckets(string from, string to)
        {
            if (!DateTime.TryParse(from, out var start) || !DateTime.TryParse(to, out var end)) return false;
            return (end.Date - start.Date).Days > 62;
        }
        private static bool Match(string? filter, string? value) => string.IsNullOrWhiteSpace(filter) || Same(filter, value ?? string.Empty);

        private static double Percent(double value, double total)
        {
            if (total <= 0) return 0;
            return Math.Round(Math.Max(0, Math.Min(100, value / total * 100)), 2);
        }

        private static double WeightedScore(double completion, double quality, double workload)
        {
            var score = completion * 0.50 + quality * 0.30 + workload * 0.20;
            return Math.Round(Math.Max(0, Math.Min(100, score)), 2);
        }

        private static string? NormalizeSection(string? section)
        {
            var clean = section?.Trim().ToLowerInvariant() ?? string.Empty;
            return clean switch
            {
                "development" or "developer" or "dev" => "Development",
                "stores" or "store" or "inventory" => "Stores",
                "qc" or "quality" or "cpi" => "QC",
                "gatepass" or "dispatch" or "advice note" => "Gatepass",
                "worker" or "workers" => "Worker",
                _ => null
            };
        }

        private static string GuessDepartmentFromRole(string? role)
        {
            var clean = role?.Trim() ?? string.Empty;
            return NormalizeSection(clean) ?? clean;
        }

        private static List<string> Distinct(IEnumerable<string?> values)
        {
            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToList();
        }

        private static List<string> RolesForSection(string section)
        {
            return section switch
            {
                "Development" => new List<string> { "Developer", "Admin" },
                "Stores" => new List<string> { "Stores", "Admin" },
                "QC" => new List<string> { "QC", "Admin" },
                "Gatepass" => new List<string> { "Gatepass", "Admin" },
                "Worker" => new List<string> { "Worker", "Admin" },
                "Users" => new List<string> { "Admin", "Developer", "QC", "Gatepass", "Audit", "Stores", "Worker" },
                _ => new List<string> { "Admin", "Developer", "QC", "Gatepass", "Audit", "Stores", "Worker" }
            };
        }

        private static List<string> StatusesForSection(string section)
        {
            return section switch
            {
                "Development" => new List<string> { "Sample created", "Client approved, not submitted", "Awaiting admin", "Admin approved", "Admin rejected" },
                "Stores" => new List<string> { "Fully issued", "Partially issued", "Not issued" },
                "QC" => new List<string> { "Passed", "Failed", "Pending" },
                "Gatepass" => new List<string> { "Fully dispatched", "Balance pending" },
                "Worker" => new List<string> { "Fully allocated", "Pending", "Completed" },
                _ => new List<string>()
            };
        }

        private static List<string> WorkerTimeSlots() => new()
        {
            "08:30-09:30", "09:30-10:30", "10:30-11:00", "11:00-12:00", "12:00-13:00", "13:00-13:30", "13:30-14:30", "14:30-15:30", "15:30-16:30", "16:30-17:30", "17:30-18:30"
        };

        private static bool LogBelongsToUser(ActivityLog log, User user)
        {
            if (!string.IsNullOrWhiteSpace(log.UserId) && log.UserId == user.Id) return true;
            if (!string.IsNullOrWhiteSpace(log.UserName) && log.UserName.Contains(user.Username, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrWhiteSpace(log.UserName) && log.UserName.Contains(user.Name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public class ManagementOverviewDto
    {
        public string GeneratedAt { get; set; } = string.Empty;
        public string DateFrom { get; set; } = string.Empty;
        public string DateTo { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<MetricCardDto> HeadlineCards { get; set; } = new();
        public List<DepartmentSummaryDto> Departments { get; set; } = new();
        public List<ReportChartDto> Charts { get; set; } = new();
    }

    public class SectionReportDto
    {
        public string Section { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GeneratedAt { get; set; } = string.Empty;
        public string DateFrom { get; set; } = string.Empty;
        public string DateTo { get; set; } = string.Empty;
        public List<MetricCardDto> Metrics { get; set; } = new();
        public List<ReportChartDto> Charts { get; set; } = new();
        public List<EmployeePerformanceDto> EmployeeRows { get; set; } = new();
        public List<ReportColumnDto> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRows { get; set; }
        public int TotalPages { get; set; }
        public List<string> Notes { get; set; } = new();
        public List<AppliedFilterDto> AppliedFilters { get; set; } = new();
    }

    public class DepartmentSummaryDto
    {
        public string Section { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PrimaryMetric { get; set; } = string.Empty;
        public string SecondaryMetric { get; set; } = string.Empty;
        public string CompletedLabel { get; set; } = string.Empty;
        public double CompletedValue { get; set; }
        public string PendingLabel { get; set; } = string.Empty;
        public double PendingValue { get; set; }
        public double CompletionPercent { get; set; }
        public double WorkloadValue { get; set; }
        public double WorkloadSharePercent { get; set; }
        public double PerformanceScore { get; set; }
    }

    public class MetricCardDto
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class ReportChartDto
    {
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = "bar"; // bar, pie, line
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ChartPointDto> Points { get; set; } = new();
    }

    public class ChartPointDto
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Value2 { get; set; }
        public double Value3 { get; set; }
        public string Detail { get; set; } = string.Empty;
    }

    public class ReportColumnDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
    }

    public class EmployeePerformanceDto
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public int OutputQty { get; set; }
        public int CompletedQty { get; set; }
        public double PendingQty { get; set; }
        public double QualityQty { get; set; }
        public double DefectQty { get; set; }
        public double Score { get; set; }
        public string Basis { get; set; } = string.Empty;
    }

    public class UserReportRowDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public string LastLogin { get; set; } = string.Empty;
        public string LastActivity { get; set; } = string.Empty;
    }

    public class ReportFilterOptionsDto
    {
        public List<string> Sections { get; set; } = new();
        public List<string> Roles { get; set; } = new();
        public List<string> Employees { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
        public List<string> Styles { get; set; } = new();
        public List<string> Customers { get; set; } = new();
        public List<string> Schedules { get; set; } = new();
        public List<string> TimeSlots { get; set; } = new();
    }

    public class AppliedFilterDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    internal class ReportQueryFilter
    {
        public string DateFrom { get; set; } = string.Empty;
        public string DateTo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Employee { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string TimeSlot { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        public List<AppliedFilterDto> ToAppliedFilters()
        {
            var rows = new List<AppliedFilterDto>
            {
                new() { Label = "From", Value = DateFrom },
                new() { Label = "To", Value = DateTo }
            };
            Add(rows, "Role", Role);
            Add(rows, "Employee", Employee);
            Add(rows, "Status", Status);
            Add(rows, "Style", StyleNo);
            Add(rows, "Customer", Customer);
            Add(rows, "Schedule", ScheduleNo);
            Add(rows, "Time Slot", TimeSlot);
            return rows;
        }

        private static void Add(List<AppliedFilterDto> rows, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) rows.Add(new AppliedFilterDto { Label = label, Value = value });
        }
    }

    internal class ReportSnapshot
    {
        public List<SampleStyle> SampleStyles { get; set; } = new();
        public List<StoreInRecord> StoreInRecords { get; set; } = new();
        public List<StoreProductionRecord> ProductionRecords { get; set; } = new();
        public List<CPIReport> CpiReports { get; set; } = new();
        public List<AdviceNoteRecord> AdviceNotes { get; set; } = new();
        public List<DailyOutputRecord> DailyOutputs { get; set; } = new();
        public List<ActivityLog> ActivityLogs { get; set; } = new();
    }
}
