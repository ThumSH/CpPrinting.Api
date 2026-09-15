using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Stores,Gatepass,Admin,Developer,QC")]
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
                    JobNo = record.JobNo ?? string.Empty,
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
                    JobNo = note.JobNo ?? string.Empty,
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

            var customerName = request.CustomerName.Trim();
            var styleNo = request.StyleNo.Trim();
            var component = request.Component.Trim();
            var scheduleNo = request.ScheduleNo?.Trim() ?? string.Empty;
            var jobNos = request.JobNos?.Trim() ?? string.Empty;
            var invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
            var poNo = request.PoNo?.Trim() ?? string.Empty;
            var colour = request.Colour?.Trim() ?? string.Empty;

            var receivedQty = request.Totals?.ReceivedQty ?? 0;
            var sentTotal = request.Totals?.SentTotal ?? 0;
            var pdTotal = request.Totals?.PdTotal ?? 0;
            var fdTotal = request.Totals?.FdTotal ?? 0;
            var sampleTestingTotal = request.Totals?.SampleTestingTotal ?? 0;
            var rtnTotal = request.Totals?.RtnTotal ?? 0;
            var goodQtyTotal = request.Totals?.GoodQtyTotal ?? 0;
            var rowsJson = JsonSerializer.Serialize(request.Rows, JsonOptions);

            // Same report identity = same customer/style/component/schedule/colour.
            // Job Nos, Invoice No, PO No, totals and rows are treated as report content that can be updated.
            var existingScopeReports = await _context.ReconciliationReports
                .AsNoTracking()
                .ToListAsync();

            var matchingScope = existingScopeReports
                .Where(r => SameReportScope(r, customerName, styleNo, component, scheduleNo, colour))
                .OrderBy(r => r.CreatedAt)
                .ToList();

            var exactDuplicate = matchingScope.FirstOrDefault(r => SameReportContent(
                r,
                jobNos,
                invoiceNo,
                poNo,
                receivedQty,
                sentTotal,
                pdTotal,
                fdTotal,
                sampleTestingTotal,
                rtnTotal,
                goodQtyTotal,
                rowsJson));

            if (exactDuplicate != null)
            {
                return Conflict(new ReconciliationSaveConflictDto
                {
                    Reason = "EXACT_DUPLICATE",
                    Message = "This exact reconciliation report is already saved. Please check Reconciliation Report Search before saving again.",
                    ExistingReportId = exactDuplicate.Id,
                    ExistingReportDate = exactDuplicate.ReportDate,
                    ExistingCreatedAt = exactDuplicate.CreatedAt,
                    ExistingUpdatedAt = exactDuplicate.UpdatedAt,
                    IsExactDuplicate = true
                });
            }

            var existingSameScope = matchingScope.FirstOrDefault();
            if (existingSameScope != null)
            {
                return Conflict(new ReconciliationSaveConflictDto
                {
                    Reason = "SAME_SCOPE_EXISTS",
                    Message = "A reconciliation report already exists for this customer, style, component, schedule and colour. Update the existing report instead of creating a duplicate.",
                    ExistingReportId = existingSameScope.Id,
                    ExistingReportDate = existingSameScope.ReportDate,
                    ExistingCreatedAt = existingSameScope.CreatedAt,
                    ExistingUpdatedAt = existingSameScope.UpdatedAt,
                    IsExactDuplicate = false
                });
            }

            var record = new ReconciliationReportRecord
            {
                Id = Guid.NewGuid().ToString(),
                CustomerName = customerName,
                StyleNo = styleNo,
                Component = component,
                ScheduleNo = scheduleNo,
                JobNos = jobNos,
                InvoiceNo = invoiceNo,
                PoNo = poNo,
                Colour = colour,
                ReportDate = reportDate,
                ReceivedQty = receivedQty,
                SentTotal = sentTotal,
                PdTotal = pdTotal,
                FdTotal = fdTotal,
                SampleTestingTotal = sampleTestingTotal,
                RtnTotal = rtnTotal,
                GoodQtyTotal = goodQtyTotal,
                RowsJson = rowsJson,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.ReconciliationReports.Add(record);
            await _context.SaveChangesAsync();

            return Ok(MapSavedReport(record));
        }

        [HttpPut("saved/{id}")]
        public async Task<ActionResult<ReconciliationSavedReportDto>> UpdateSavedReport(string id, SaveReconciliationReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName)) return BadRequest("Customer is required.");
            if (string.IsNullOrWhiteSpace(request.StyleNo)) return BadRequest("Style No is required.");
            if (string.IsNullOrWhiteSpace(request.Component)) return BadRequest("Component is required.");
            if (request.Rows == null || request.Rows.Count == 0) return BadRequest("Report rows are required.");

            var existing = await _context.ReconciliationReports.FirstOrDefaultAsync(r => r.Id == id);
            if (existing == null) return NotFound("Saved reconciliation report was not found.");

            var now = DateTime.UtcNow.ToString("o");
            var customerName = request.CustomerName.Trim();
            var styleNo = request.StyleNo.Trim();
            var component = request.Component.Trim();
            var scheduleNo = request.ScheduleNo?.Trim() ?? string.Empty;
            var jobNos = request.JobNos?.Trim() ?? string.Empty;
            var invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
            var poNo = request.PoNo?.Trim() ?? string.Empty;
            var colour = request.Colour?.Trim() ?? string.Empty;

            if (!SameReportScope(existing, customerName, styleNo, component, scheduleNo, colour))
            {
                return BadRequest("The selected saved report does not match the current customer, style, component, schedule and colour scope.");
            }

            var receivedQty = request.Totals?.ReceivedQty ?? 0;
            var sentTotal = request.Totals?.SentTotal ?? 0;
            var pdTotal = request.Totals?.PdTotal ?? 0;
            var fdTotal = request.Totals?.FdTotal ?? 0;
            var sampleTestingTotal = request.Totals?.SampleTestingTotal ?? 0;
            var rtnTotal = request.Totals?.RtnTotal ?? 0;
            var goodQtyTotal = request.Totals?.GoodQtyTotal ?? 0;
            var rowsJson = JsonSerializer.Serialize(request.Rows, JsonOptions);

            var allReports = await _context.ReconciliationReports
                .AsNoTracking()
                .ToListAsync();

            var duplicateInAnotherRecord = allReports.FirstOrDefault(r =>
                r.Id != existing.Id &&
                SameReportScope(r, customerName, styleNo, component, scheduleNo, colour) &&
                SameReportContent(
                    r,
                    jobNos,
                    invoiceNo,
                    poNo,
                    receivedQty,
                    sentTotal,
                    pdTotal,
                    fdTotal,
                    sampleTestingTotal,
                    rtnTotal,
                    goodQtyTotal,
                    rowsJson));

            if (duplicateInAnotherRecord != null)
            {
                return Conflict(new ReconciliationSaveConflictDto
                {
                    Reason = "EXACT_DUPLICATE",
                    Message = "Another identical reconciliation report already exists. Please check Reconciliation Report Search before updating.",
                    ExistingReportId = duplicateInAnotherRecord.Id,
                    ExistingReportDate = duplicateInAnotherRecord.ReportDate,
                    ExistingCreatedAt = duplicateInAnotherRecord.CreatedAt,
                    ExistingUpdatedAt = duplicateInAnotherRecord.UpdatedAt,
                    IsExactDuplicate = true
                });
            }

            // Keep the original ReportDate as the first saved date. UpdatedAt records later changes.
            existing.CustomerName = customerName;
            existing.StyleNo = styleNo;
            existing.Component = component;
            existing.ScheduleNo = scheduleNo;
            existing.JobNos = jobNos;
            existing.InvoiceNo = invoiceNo;
            existing.PoNo = poNo;
            existing.Colour = colour;
            existing.ReceivedQty = receivedQty;
            existing.SentTotal = sentTotal;
            existing.PdTotal = pdTotal;
            existing.FdTotal = fdTotal;
            existing.SampleTestingTotal = sampleTestingTotal;
            existing.RtnTotal = rtnTotal;
            existing.GoodQtyTotal = goodQtyTotal;
            existing.RowsJson = rowsJson;
            existing.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return Ok(MapSavedReport(existing));
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

        private static string NormalizeCompare(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;
            return Regex.Replace(trimmed, @"\s+", " ").ToUpperInvariant();
        }

        private static bool SameReportScope(
            ReconciliationReportRecord record,
            string customerName,
            string styleNo,
            string component,
            string scheduleNo,
            string colour)
        {
            return NormalizeCompare(record.CustomerName) == NormalizeCompare(customerName) &&
                   NormalizeCompare(record.StyleNo) == NormalizeCompare(styleNo) &&
                   NormalizeCompare(record.Component) == NormalizeCompare(component) &&
                   NormalizeCompare(record.ScheduleNo) == NormalizeCompare(scheduleNo) &&
                   NormalizeCompare(record.Colour) == NormalizeCompare(colour);
        }

        private static bool SameReportContent(
            ReconciliationReportRecord record,
            string jobNos,
            string invoiceNo,
            string poNo,
            int receivedQty,
            int sentTotal,
            int pdTotal,
            int fdTotal,
            int sampleTestingTotal,
            int rtnTotal,
            int goodQtyTotal,
            string rowsJson)
        {
            return NormalizeCompare(record.JobNos) == NormalizeCompare(jobNos) &&
                   NormalizeCompare(record.InvoiceNo) == NormalizeCompare(invoiceNo) &&
                   NormalizeCompare(record.PoNo) == NormalizeCompare(poNo) &&
                   record.ReceivedQty == receivedQty &&
                   record.SentTotal == sentTotal &&
                   record.PdTotal == pdTotal &&
                   record.FdTotal == fdTotal &&
                   record.SampleTestingTotal == sampleTestingTotal &&
                   record.RtnTotal == rtnTotal &&
                   record.GoodQtyTotal == goodQtyTotal &&
                   (record.RowsJson ?? "[]") == rowsJson;
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
                JobNos = record.JobNos,
                InvoiceNo = record.InvoiceNo,
                PoNo = record.PoNo,
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

    public class ReconciliationSaveConflictDto
    {
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ExistingReportId { get; set; } = string.Empty;
        public string ExistingReportDate { get; set; } = string.Empty;
        public string ExistingCreatedAt { get; set; } = string.Empty;
        public string ExistingUpdatedAt { get; set; } = string.Empty;
        public bool IsExactDuplicate { get; set; }
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
        public string JobNo { get; set; } = string.Empty;
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
        public string JobNo { get; set; } = string.Empty;
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
        public string? JobNos { get; set; }
        public string? InvoiceNo { get; set; }
        public string? PoNo { get; set; }
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
        public string JobNos { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
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
        public string ReceivedJobNo { get; set; } = string.Empty;
        public string ReceivedAdNo { get; set; } = string.Empty;
        public string ReceivedCutNo { get; set; } = string.Empty;
        public int? ReceivedQty { get; set; }
        public int? ReceivedRunningTotal { get; set; }

        public string SentDate { get; set; } = string.Empty;
        public string SentJobNo { get; set; } = string.Empty;
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