using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Worker,Admin")]
    [Route("api/worker-cut-reports")]
    [ApiController]
    public class WorkerCutReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public WorkerCutReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkerCutReportDto>>> GetReports(
            [FromQuery] string? dateFrom,
            [FromQuery] string? dateTo,
            [FromQuery] string? storeInRecordId,
            [FromQuery] string? styleNo,
            [FromQuery] string? customerName,
            [FromQuery] string? cutNo,
            [FromQuery] string? inAdNo,
            [FromQuery] string? scheduleNo,
            [FromQuery] string? jobNo)
        {
            var query = _context.WorkerCutReports.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(storeInRecordId))
            {
                var value = storeInRecordId.Trim();
                query = query.Where(r => r.StoreInRecordId == value);
            }

            if (!string.IsNullOrWhiteSpace(styleNo))
            {
                var value = styleNo.Trim().ToLower();
                query = query.Where(r => r.StyleNo.ToLower().Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var value = customerName.Trim().ToLower();
                query = query.Where(r => r.CustomerName.ToLower().Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(cutNo))
            {
                var value = cutNo.Trim().ToLower();
                query = query.Where(r => r.CutNo.ToLower().Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(inAdNo))
            {
                var value = inAdNo.Trim().ToLower();
                query = query.Where(r => r.InAdNo.ToLower().Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(scheduleNo))
            {
                var value = scheduleNo.Trim().ToLower();
                query = query.Where(r => r.ScheduleNo.ToLower().Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(jobNo))
            {
                var value = jobNo.Trim().ToLower();
                query = query.Where(r => r.JobNo.ToLower().Contains(value));
            }

            var reports = await query
                .OrderByDescending(r => r.ReportDate)
                .ThenByDescending(r => r.UpdatedAt)
                .ToListAsync();

            // ReportDate is stored as yyyy-MM-dd. Apply range in memory to avoid provider-specific string comparison issues.
            if (!string.IsNullOrWhiteSpace(dateFrom))
                reports = reports.Where(r => string.CompareOrdinal(r.ReportDate, dateFrom.Trim()) >= 0).ToList();

            if (!string.IsNullOrWhiteSpace(dateTo))
                reports = reports.Where(r => string.CompareOrdinal(r.ReportDate, dateTo.Trim()) <= 0).ToList();

            return Ok(reports.Select(MapReport));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WorkerCutReportDto>> GetReport(string id)
        {
            var report = await _context.WorkerCutReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound("Worker cut report was not found.");
            return Ok(MapReport(report));
        }

        [HttpPost]
        public async Task<ActionResult<WorkerCutReportDto>> SaveReport(SaveWorkerCutReportRequest request)
        {
            var validationError = ValidateRequest(request);
            if (validationError != null) return BadRequest(validationError);

            var storeInRecordId = request.StoreInRecordId.Trim();
            var cutNo = request.CutNo.Trim();
            var now = DateTime.UtcNow.ToString("o");

            var existing = await _context.WorkerCutReports.FirstOrDefaultAsync(r =>
                r.StoreInRecordId == storeInRecordId &&
                r.CutNo == cutNo);

            if (existing == null)
            {
                existing = new WorkerCutReport
                {
                    Id = Guid.NewGuid().ToString(),
                    StoreInRecordId = storeInRecordId,
                    CutNo = cutNo,
                    CreatedAt = now
                };

                _context.WorkerCutReports.Add(existing);
            }

            ApplyRequest(existing, request, now);
            await _context.SaveChangesAsync();

            return Ok(MapReport(existing));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WorkerCutReportDto>> UpdateReport(string id, SaveWorkerCutReportRequest request)
        {
            var validationError = ValidateRequest(request);
            if (validationError != null) return BadRequest(validationError);

            var report = await _context.WorkerCutReports.FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound("Worker cut report was not found.");

            var now = DateTime.UtcNow.ToString("o");
            ApplyRequest(report, request, now);
            await _context.SaveChangesAsync();

            return Ok(MapReport(report));
        }

        private static string? ValidateRequest(SaveWorkerCutReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.StoreInRecordId)) return "Store-In record ID is required.";
            if (string.IsNullOrWhiteSpace(request.CutNo)) return "Cut No is required.";
            if (string.IsNullOrWhiteSpace(request.StyleNo)) return "Style No is required.";
            if (request.Rows == null || request.Rows.Count == 0) return "At least one bundle row is required.";
            return null;
        }

        private static void ApplyRequest(WorkerCutReport report, SaveWorkerCutReportRequest request, string now)
        {
            report.StoreInRecordId = request.StoreInRecordId.Trim();
            report.ProductionRecordId = request.ProductionRecordId?.Trim() ?? string.Empty;
            report.SubmissionId = request.SubmissionId?.Trim() ?? string.Empty;
            report.RevisionNo = request.RevisionNo;
            report.StyleNo = request.StyleNo.Trim();
            report.CustomerName = request.CustomerName?.Trim() ?? string.Empty;
            report.BodyColour = request.BodyColour?.Trim() ?? string.Empty;
            report.PrintColour = request.PrintColour?.Trim() ?? string.Empty;
            report.Component = request.Component?.Trim() ?? string.Empty;
            report.Season = request.Season?.Trim() ?? string.Empty;
            report.InAdNo = request.InAdNo?.Trim() ?? string.Empty;
            report.ScheduleNo = request.ScheduleNo?.Trim() ?? string.Empty;
            report.JobNo = request.JobNo?.Trim() ?? string.Empty;
            report.CutInDate = request.CutInDate?.Trim() ?? string.Empty;
            report.InQty = request.InQty;
            report.TotalCutQty = request.TotalCutQty;
            report.CutNo = request.CutNo.Trim();
            report.CutQty = request.CutQty;
            report.BundleCount = request.BundleCount > 0 ? request.BundleCount : request.Rows.Count;
            report.ReportDate = string.IsNullOrWhiteSpace(request.ReportDate)
                ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                : request.ReportDate.Trim();
            report.WorkerName = request.WorkerName?.Trim() ?? string.Empty;
            report.RowsJson = JsonSerializer.Serialize(request.Rows, JsonOptions);
            report.UpdatedAt = now;
        }

        private static WorkerCutReportDto MapReport(WorkerCutReport report)
        {
            var rows = new List<WorkerCutReportRowDto>();
            try
            {
                rows = JsonSerializer.Deserialize<List<WorkerCutReportRowDto>>(report.RowsJson ?? "[]", JsonOptions) ?? new();
            }
            catch
            {
                rows = new();
            }

            return new WorkerCutReportDto
            {
                Id = report.Id,
                StoreInRecordId = report.StoreInRecordId,
                ProductionRecordId = report.ProductionRecordId,
                SubmissionId = report.SubmissionId,
                RevisionNo = report.RevisionNo,
                StyleNo = report.StyleNo,
                CustomerName = report.CustomerName,
                BodyColour = report.BodyColour,
                PrintColour = report.PrintColour,
                Component = report.Component,
                Season = report.Season,
                InAdNo = report.InAdNo,
                ScheduleNo = report.ScheduleNo,
                JobNo = report.JobNo,
                CutInDate = report.CutInDate,
                InQty = report.InQty,
                TotalCutQty = report.TotalCutQty,
                CutNo = report.CutNo,
                CutQty = report.CutQty,
                BundleCount = report.BundleCount,
                ReportDate = report.ReportDate,
                WorkerName = report.WorkerName,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt,
                Rows = rows
            };
        }
    }

    public class SaveWorkerCutReportRequest
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string? ProductionRecordId { get; set; }
        public string? SubmissionId { get; set; }
        public int RevisionNo { get; set; }
        public string StyleNo { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? BodyColour { get; set; }
        public string? PrintColour { get; set; }
        public string? Component { get; set; }
        public string? Season { get; set; }
        public string? InAdNo { get; set; }
        public string? ScheduleNo { get; set; }
        public string? JobNo { get; set; }
        public string? CutInDate { get; set; }
        public int InQty { get; set; }
        public int TotalCutQty { get; set; }
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }
        public int BundleCount { get; set; }
        public string? ReportDate { get; set; }
        public string? WorkerName { get; set; }
        public List<WorkerCutReportRowDto> Rows { get; set; } = new();
    }

    public class WorkerCutReportDto : SaveWorkerCutReportRequest
    {
        public string Id { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class WorkerCutReportRowDto
    {
        public string BundleId { get; set; } = string.Empty;
        public string BundleNo { get; set; } = string.Empty;
        public int BundleQty { get; set; }
        public string Size { get; set; } = string.Empty;
        public string NumberRange { get; set; } = string.Empty;
        public int BundleOrder { get; set; }
        public bool ProductionIn { get; set; }
        public bool ProductionOut { get; set; }
        public bool HandedOverToQc { get; set; }
        public bool CheckingStatus { get; set; }
        public bool CuringStatus { get; set; }
    }
}
