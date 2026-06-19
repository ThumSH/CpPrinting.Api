using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Stores,Gatepass,Admin,Developer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ReconciliationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public ReconciliationController(AppDbContext context)
        {
            _context = context;
        }

        // Read-only data source for generating a fresh Reconciliation Report.
        // IMPORTANT: This does not modify Store-In, Gatepass, stock, CPI, or Advice Note logic.
        [HttpGet("report-source")]
        public async Task<ActionResult<ReconciliationSourceDto>> GetReportSource()
        {
            var storeIns = await _context.StoreInRecords
                .Include(r => r.Cuts)
                .OrderBy(r => r.CutInDate)
                .ThenBy(r => r.InAdNo)
                .ToListAsync();

            var adviceNotes = await _context.AdviceNotes
                .OrderBy(n => n.DeliveryDate)
                .ThenBy(n => n.AdNo)
                .ToListAsync();

            var result = new ReconciliationSourceDto
            {
                StoreIns = storeIns.Select(record => new ReconciliationStoreInDto
                {
                    Id = record.Id,
                    SubmissionId = record.SubmissionId,
                    RevisionNo = record.RevisionNo,
                    StyleNo = record.StyleNo ?? string.Empty,
                    CustomerName = record.CustomerName ?? string.Empty,
                    BodyColour = record.BodyColour ?? string.Empty,
                    PrintColour = record.PrintColour ?? string.Empty,
                    Components = record.Components ?? string.Empty,
                    Season = record.Season ?? string.Empty,
                    InAdNo = record.InAdNo ?? string.Empty,
                    ScheduleNo = record.ScheduleNo ?? string.Empty,
                    CutInDate = record.CutInDate ?? string.Empty,
                    InQty = record.InQty,
                    TotalCutQty = record.TotalCutQty,
                    Cuts = record.Cuts.Select(cut => new ReconciliationStoreInCutDto
                    {
                        Id = cut.Id,
                        CutNo = cut.CutNo,
                        CutQty = cut.CutQty,
                        SubmissionId = cut.SubmissionId
                    }).ToList()
                }).ToList(),

                AdviceNotes = adviceNotes.Select(note => new ReconciliationAdviceNoteDto
                {
                    Id = note.Id,
                    StoreInRecordId = note.StoreInRecordId,
                    SubmissionId = note.SubmissionId,
                    RevisionNo = note.RevisionNo,
                    AdviceNoteAdNo = note.AdNo,
                    DeliveryDate = note.DeliveryDate,
                    CustomerName = note.CustomerName,
                    StyleNo = note.StyleNo,
                    ScheduleNo = note.ScheduleNo,
                    CutNo = note.CutNo,
                    Component = note.Component,
                    DispatchQty = note.DispatchQty,
                    Rows = (note.Rows ?? new Dictionary<string, AdviceNoteRow>())
                        .ToDictionary(
                            pair => pair.Key,
                            pair => new ReconciliationAdviceRowDto
                            {
                                Colour = pair.Value.Colour,
                                BundleNo = pair.Value.BundleNo,
                                Size = pair.Value.Size,
                                CutForm = pair.Value.CutForm,
                                Component = pair.Value.Component,
                                TotalPcs = pair.Value.TotalPcs,
                                Pd = pair.Value.Pd,
                                Fd = pair.Value.Fd,
                                GoodQty = pair.Value.GoodQty
                            })
                }).ToList()
            };

            return Ok(result);
        }

        // Save the exact report snapshot visible to the user.
        // This is a reporting snapshot only and does not update Store-In/Gatepass/inventory balances.
        [HttpPost("saved")]
        public async Task<ActionResult<ReconciliationSavedReportDto>> SaveReport(SaveReconciliationReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName)) return BadRequest("Customer is required.");
            if (string.IsNullOrWhiteSpace(request.StyleNo)) return BadRequest("Style No is required.");
            if (string.IsNullOrWhiteSpace(request.Component)) return BadRequest("Component is required.");
            if (request.Rows == null || request.Rows.Count == 0) return BadRequest("Report rows are required.");

            var now = DateTime.UtcNow.ToString("o");
            var reportDate = string.IsNullOrWhiteSpace(request.ReportDate)
                ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                : request.ReportDate.Trim();

            var record = new ReconciliationReportRecord
            {
                Id = Guid.NewGuid().ToString(),
                CustomerName = request.CustomerName.Trim(),
                StyleNo = request.StyleNo.Trim(),
                Component = request.Component.Trim(),
                ScheduleNo = request.ScheduleNo?.Trim() ?? string.Empty,
                Colour = request.Colour?.Trim() ?? string.Empty,
                ReportDate = reportDate,
                ReceivedQty = request.Totals?.ReceivedQty ?? 0,
                SentTotal = request.Totals?.SentTotal ?? 0,
                PdTotal = request.Totals?.PdTotal ?? 0,
                FdTotal = request.Totals?.FdTotal ?? 0,
                SampleTestingTotal = request.Totals?.SampleTestingTotal ?? 0,
                RtnTotal = request.Totals?.RtnTotal ?? 0,
                GoodQtyTotal = request.Totals?.GoodQtyTotal ?? 0,
                RowsJson = JsonSerializer.Serialize(request.Rows, JsonOptions),
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.ReconciliationReports.Add(record);
            await _context.SaveChangesAsync();

            return Ok(MapSavedReport(record));
        }

        // Search saved report snapshots.
        [HttpGet("saved")]
        public async Task<ActionResult<IEnumerable<ReconciliationSavedReportDto>>> GetSavedReports(
            [FromQuery] string? customerName,
            [FromQuery] string? styleNo,
            [FromQuery] string? component,
            [FromQuery] string? dateFrom,
            [FromQuery] string? dateTo)
        {
            var query = _context.ReconciliationReports.AsQueryable();

            if (!string.IsNullOrWhiteSpace(customerName))
                query = query.Where(r => r.CustomerName == customerName);

            if (!string.IsNullOrWhiteSpace(styleNo))
                query = query.Where(r => r.StyleNo == styleNo);

            if (!string.IsNullOrWhiteSpace(component))
                query = query.Where(r => r.Component == component);

            var reports = await query
                .OrderByDescending(r => r.ReportDate)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            // ReportDate is stored as yyyy-MM-dd. Apply date range in memory to avoid provider-specific string comparison translation issues.
            if (!string.IsNullOrWhiteSpace(dateFrom))
                reports = reports.Where(r => string.CompareOrdinal(r.ReportDate, dateFrom) >= 0).ToList();

            if (!string.IsNullOrWhiteSpace(dateTo))
                reports = reports.Where(r => string.CompareOrdinal(r.ReportDate, dateTo) <= 0).ToList();

            return Ok(reports.Select(MapSavedReport));
        }

        [HttpGet("saved/{id}")]
        public async Task<ActionResult<ReconciliationSavedReportDto>> GetSavedReport(string id)
        {
            var report = await _context.ReconciliationReports.FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound();
            return Ok(MapSavedReport(report));
        }

        [HttpDelete("saved/{id}")]
        public async Task<IActionResult> DeleteSavedReport(string id)
        {
            var report = await _context.ReconciliationReports.FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound();

            _context.ReconciliationReports.Remove(report);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static ReconciliationSavedReportDto MapSavedReport(ReconciliationReportRecord record)
        {
            var rows = new List<ReconciliationSavedRowDto>();
            try
            {
                rows = JsonSerializer.Deserialize<List<ReconciliationSavedRowDto>>(record.RowsJson ?? "[]", JsonOptions) ?? new();
            }
            catch
            {
                rows = new();
            }

            return new ReconciliationSavedReportDto
            {
                Id = record.Id,
                CustomerName = record.CustomerName,
                StyleNo = record.StyleNo,
                Component = record.Component,
                ScheduleNo = record.ScheduleNo,
                Colour = record.Colour,
                ReportDate = record.ReportDate,
                CreatedAt = record.CreatedAt,
                UpdatedAt = record.UpdatedAt,
                Totals = new ReconciliationReportTotalsDto
                {
                    ReceivedQty = record.ReceivedQty,
                    SentTotal = record.SentTotal,
                    PdTotal = record.PdTotal,
                    FdTotal = record.FdTotal,
                    SampleTestingTotal = record.SampleTestingTotal,
                    RtnTotal = record.RtnTotal,
                    GoodQtyTotal = record.GoodQtyTotal
                },
                Rows = rows
            };
        }
    }

    public class ReconciliationSourceDto
    {
        public List<ReconciliationStoreInDto> StoreIns { get; set; } = new();
        public List<ReconciliationAdviceNoteDto> AdviceNotes { get; set; } = new();
    }

    public class ReconciliationStoreInDto
    {
        public string Id { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string InAdNo { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutInDate { get; set; } = string.Empty;
        public int InQty { get; set; }
        public int TotalCutQty { get; set; }
        public List<ReconciliationStoreInCutDto> Cuts { get; set; } = new();
    }

    public class ReconciliationStoreInCutDto
    {
        public string Id { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }
        public string SubmissionId { get; set; } = string.Empty;
    }

    public class ReconciliationAdviceNoteDto
    {
        public string Id { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        // Kept only for traceability. The report table does NOT use this as AD No.
        public string AdviceNoteAdNo { get; set; } = string.Empty;

        public string DeliveryDate { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public int DispatchQty { get; set; }
        public Dictionary<string, ReconciliationAdviceRowDto> Rows { get; set; } = new();
    }

    public class ReconciliationAdviceRowDto
    {
        public string Colour { get; set; } = string.Empty;
        public string BundleNo { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string CutForm { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public int TotalPcs { get; set; }
        public int Pd { get; set; }
        public int Fd { get; set; }
        public int GoodQty { get; set; }
    }

    public class SaveReconciliationReportRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string? ScheduleNo { get; set; }
        public string? Colour { get; set; }
        public string? ReportDate { get; set; }
        public ReconciliationReportTotalsDto? Totals { get; set; }
        public List<ReconciliationSavedRowDto> Rows { get; set; } = new();
    }

    public class ReconciliationSavedReportDto
    {
        public string Id { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        public string ReportDate { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public ReconciliationReportTotalsDto Totals { get; set; } = new();
        public List<ReconciliationSavedRowDto> Rows { get; set; } = new();
    }

    public class ReconciliationReportTotalsDto
    {
        public int ReceivedQty { get; set; }
        public int SentTotal { get; set; }
        public int PdTotal { get; set; }
        public int FdTotal { get; set; }
        public int SampleTestingTotal { get; set; }
        public int RtnTotal { get; set; }
        public int GoodQtyTotal { get; set; }
    }

    public class ReconciliationSavedRowDto
    {
        public string ReceivedDate { get; set; } = string.Empty;
        public string ReceivedAdNo { get; set; } = string.Empty;
        public string ReceivedCutNo { get; set; } = string.Empty;
        public int? ReceivedQty { get; set; }
        public int? ReceivedRunningTotal { get; set; }

        public string SentDate { get; set; } = string.Empty;
        public string SentAdNo { get; set; } = string.Empty;
        public string SentCutNo { get; set; } = string.Empty;
        public int? SentTotal { get; set; }
        public int? Pd { get; set; }
        public int? Fd { get; set; }
        public int? SampleTesting { get; set; }
        public int? Rtn { get; set; }
        public int? GoodQty { get; set; }
        public int? GoodTotal { get; set; }
    }
}
