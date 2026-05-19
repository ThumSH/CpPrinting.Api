using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CpPrinting.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Worker,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public WorkerController(AppDbContext context, ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("eligible-styles")]
        public async Task<ActionResult> GetEligibleStyles()
        {
            var productionRecords = await _context.StoreProductionRecords
                .OrderByDescending(p => p.IssueDate)
                .ToListAsync();

            if (!productionRecords.Any())
                return Ok(Array.Empty<object>());

            var storeInIds = productionRecords.Select(p => p.StoreInRecordId).Distinct().ToList();
            var storeInRecords = await _context.StoreInRecords
                .Where(s => storeInIds.Contains(s.Id))
                .ToListAsync();
            var storeInMap = storeInRecords.ToDictionary(s => s.Id);

            var cpiReports = await _context.CpiReports
                .Where(r => storeInIds.Contains(r.StoreInRecordId))
                .ToListAsync();
            var cpiByStoreIn = cpiReports.ToDictionary(r => r.StoreInRecordId);

            var dailyOutputs = await _context.DailyOutputRecords
                .Where(d => !string.IsNullOrWhiteSpace(d.ProductionRecordId))
                .Select(d => new
                {
                    d.ProductionRecordId,
                    d.TotalSeating,
                    d.TotalPrinting,
                    d.TotalCuring,
                    d.TotalChecking,
                    d.TotalPacking,
                    d.TotalDispatch
                })
                .ToListAsync();

            // FIXED: Calculate 'Completed' as the SUM of all pieces distributed across all process stages
            var completedByProduction = dailyOutputs
                .GroupBy(d => d.ProductionRecordId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.TotalSeating + x.TotalPrinting + x.TotalCuring + x.TotalChecking + x.TotalPacking + x.TotalDispatch)
                );

            var result = productionRecords.Select(p =>
            {
                storeInMap.TryGetValue(p.StoreInRecordId, out var storeIn);

                var completed = completedByProduction.GetValueOrDefault(p.Id, 0);
                var remainingQty = Math.Max(0, p.IssueQty - completed);

                var resolvedComponent = p.Components;
                if (string.IsNullOrWhiteSpace(resolvedComponent))
                {
                    cpiByStoreIn.TryGetValue(p.StoreInRecordId, out var cpiForCut);
                    var cpiCut = cpiForCut?.CutInspections?.FirstOrDefault(ci => ci.CutNo == p.CutNo);
                    resolvedComponent = cpiCut?.Part ?? string.Empty;
                }

                return new
                {
                    p.Id,                                          
                    ProductionRecordId = p.Id,                     
                    StoreInRecordId = p.StoreInRecordId,
                    p.SubmissionId,
                    StyleNo = p.StyleNo ?? string.Empty,
                    CustomerName = p.CustomerName ?? string.Empty,
                    ScheduleNo = storeIn?.ScheduleNo ?? string.Empty,
                    Components = resolvedComponent,                
                    Component = resolvedComponent,                 
                    BodyColour = storeIn?.BodyColour ?? string.Empty,
                    CutNo = p.CutNo ?? string.Empty,
                    LineNo = p.LineNo ?? string.Empty,
                    IssueDate = p.IssueDate ?? string.Empty,
                    OriginalQty = p.IssueQty,                      
                    DispatchedQty = completed,                 
                    OrderQty = remainingQty,   
                };
            })
            .Where(x => x.OrderQty > 0)                            
            .ToList();

            return Ok(result);
        }

        [HttpGet("daily-output")]
        public async Task<ActionResult> GetDailyOutputRecords(
            [FromQuery] bool paginated = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? styleNo = null,
            [FromQuery] string? customerName = null,
            [FromQuery] string? cutNo = null,
            [FromQuery] string? component = null,
            [FromQuery] string? workerName = null,
            [FromQuery] string? tableNo = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null)
        {
            var query = _context.DailyOutputRecords.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(r =>
                    (r.StyleNo != null && r.StyleNo.ToLower().Contains(s)) ||
                    (r.CustomerName != null && r.CustomerName.ToLower().Contains(s)) ||
                    (r.CutNo != null && r.CutNo.ToLower().Contains(s)) ||
                    (r.WorkerName != null && r.WorkerName.ToLower().Contains(s)) ||
                    (r.TableNo != null && r.TableNo.ToLower().Contains(s)) ||
                    (r.Component != null && r.Component.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(styleNo))      query = query.Where(r => r.StyleNo == styleNo);
            if (!string.IsNullOrWhiteSpace(customerName)) query = query.Where(r => r.CustomerName == customerName);
            if (!string.IsNullOrWhiteSpace(cutNo))        query = query.Where(r => r.CutNo == cutNo);
            if (!string.IsNullOrWhiteSpace(component))    query = query.Where(r => r.Component == component);
            if (!string.IsNullOrWhiteSpace(workerName))   query = query.Where(r => r.WorkerName == workerName);
            if (!string.IsNullOrWhiteSpace(tableNo))      query = query.Where(r => r.TableNo == tableNo);

            if (!string.IsNullOrWhiteSpace(dateFrom))
                query = query.Where(r => r.Date != null && string.Compare(r.Date, dateFrom) >= 0);
            if (!string.IsNullOrWhiteSpace(dateTo))
                query = query.Where(r => r.Date != null && string.Compare(r.Date, dateTo) <= 0);

            query = query.OrderByDescending(r => r.Date);

            if (!paginated)
                return Ok(await query.ToListAsync());

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PaginatedResponseDto<DailyOutputRecord>
            {
                Items = items, Total = total, Page = page, PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpPost("daily-output")]
        public async Task<ActionResult<DailyOutputRecord>> CreateDailyOutput(DailyOutputRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(record.TableNo))
                return BadRequest("TableNo is required.");

            var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);
            if (storeIn == null) return BadRequest("Linked Store-In record not found.");

            var productionRecords = await _context.StoreProductionRecords.Where(p => p.StoreInRecordId == record.StoreInRecordId).ToListAsync();
            if (!productionRecords.Any()) return BadRequest("No production records found for this Store-In.");

            if (string.IsNullOrWhiteSpace(record.ProductionRecordId)) return BadRequest("ProductionRecordId is required.");

            var productionRecord = productionRecords.FirstOrDefault(p => p.Id == record.ProductionRecordId);
            if (productionRecord == null) return BadRequest("Production record not found for this Store-In.");

            if (string.IsNullOrWhiteSpace(record.Id)) record.Id = Guid.NewGuid().ToString();

            record.SubmissionId = storeIn.SubmissionId;
            record.StyleNo = storeIn.StyleNo ?? string.Empty;
            record.CustomerName = storeIn.CustomerName ?? string.Empty;
            record.CutNo = productionRecord.CutNo ?? string.Empty;
            record.OrderQty = productionRecord.IssueQty;

            record.TotalSeating = record.TimeSlots?.Sum(t => t.Seating) ?? 0;
            record.TotalPrinting = record.TimeSlots?.Sum(t => t.Printing) ?? 0;
            record.TotalCuring = record.TimeSlots?.Sum(t => t.Curing) ?? 0;
            record.TotalChecking = record.TimeSlots?.Sum(t => t.Checking) ?? 0;
            record.TotalPacking = record.TimeSlots?.Sum(t => t.Packing) ?? 0;
            record.TotalDispatch = record.TimeSlots?.Sum(t => t.Dispatch) ?? 0;

            // FIXED: Validate stages cumulatively against IssueQty
            var existingOutputs = await _context.DailyOutputRecords
                .Where(d => d.ProductionRecordId == record.ProductionRecordId)
                .Select(d => new { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch })
                .ToListAsync();

            var previousTotal = existingOutputs.Sum(x => x.TotalSeating + x.TotalPrinting + x.TotalCuring + x.TotalChecking + x.TotalPacking + x.TotalDispatch);
            var currentTotal = record.TotalSeating + record.TotalPrinting + record.TotalCuring + record.TotalChecking + record.TotalPacking + record.TotalDispatch;

            if (previousTotal + currentTotal > productionRecord.IssueQty)
                return BadRequest($"Total pieces distributed across all stages ({previousTotal + currentTotal}) exceeds production issue qty ({productionRecord.IssueQty}).");

            _context.DailyOutputRecords.Add(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "DailyOutput", record.Id,
                $"Logged daily output for {record.StyleNo} (Cut: {record.CutNo}), Table: {record.TableNo}");

            return CreatedAtAction(nameof(GetDailyOutputRecords), new { id = record.Id }, record);
        }

        [HttpPost("daily-output/batch")]
        public async Task<ActionResult<IEnumerable<DailyOutputRecord>>> BatchCreateDailyOutput([FromBody] List<DailyOutputRecord> records)
        {
            if (records == null || records.Count == 0) return BadRequest("At least one record is required.");

            var saved = new List<DailyOutputRecord>();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StoreInRecordId)) return BadRequest("StoreInRecordId is required.");

                var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);
                if (storeIn == null) return BadRequest("Linked Store-In record not found.");

                var productionRecords = await _context.StoreProductionRecords.Where(p => p.StoreInRecordId == record.StoreInRecordId).ToListAsync();
                if (!productionRecords.Any()) return BadRequest($"No production records for Store-In '{storeIn.StyleNo}'.");

                if (string.IsNullOrWhiteSpace(record.ProductionRecordId)) return BadRequest("ProductionRecordId is required for all records.");

                var productionRecord = productionRecords.FirstOrDefault(p => p.Id == record.ProductionRecordId);
                if (productionRecord == null) return BadRequest($"Production record '{record.ProductionRecordId}' not found.");

                record.Id = Guid.NewGuid().ToString();
                record.SubmissionId = storeIn.SubmissionId;
                record.StyleNo = storeIn.StyleNo ?? string.Empty;
                record.CustomerName = storeIn.CustomerName ?? string.Empty;
                record.CutNo = productionRecord.CutNo ?? string.Empty;
                record.OrderQty = productionRecord.IssueQty;

                record.TotalSeating = record.TimeSlots?.Sum(t => t.Seating) ?? 0;
                record.TotalPrinting = record.TimeSlots?.Sum(t => t.Printing) ?? 0;
                record.TotalCuring = record.TimeSlots?.Sum(t => t.Curing) ?? 0;
                record.TotalChecking = record.TimeSlots?.Sum(t => t.Checking) ?? 0;
                record.TotalPacking = record.TimeSlots?.Sum(t => t.Packing) ?? 0;
                record.TotalDispatch = record.TimeSlots?.Sum(t => t.Dispatch) ?? 0;

                // FIXED: Validate cumulative stages for the batch record against DB AND the current batch
                var existingOutputs = await _context.DailyOutputRecords
                    .Where(d => d.ProductionRecordId == record.ProductionRecordId)
                    .Select(d => new { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch })
                    .ToListAsync();
                
                var inBatchOutputs = records
                    .Where(d => d.ProductionRecordId == record.ProductionRecordId && d != record)
                    .Select(d => new { 
                        TotalSeating = d.TimeSlots?.Sum(t => t.Seating) ?? 0, 
                        TotalPrinting = d.TimeSlots?.Sum(t => t.Printing) ?? 0, 
                        TotalCuring = d.TimeSlots?.Sum(t => t.Curing) ?? 0, 
                        TotalChecking = d.TimeSlots?.Sum(t => t.Checking) ?? 0, 
                        TotalPacking = d.TimeSlots?.Sum(t => t.Packing) ?? 0, 
                        TotalDispatch = d.TimeSlots?.Sum(t => t.Dispatch) ?? 0 
                    })
                    .ToList();

                var previousTotal = existingOutputs.Sum(x => x.TotalSeating + x.TotalPrinting + x.TotalCuring + x.TotalChecking + x.TotalPacking + x.TotalDispatch);
                var batchOthersTotal = inBatchOutputs.Sum(x => x.TotalSeating + x.TotalPrinting + x.TotalCuring + x.TotalChecking + x.TotalPacking + x.TotalDispatch);
                var currentTotal = record.TotalSeating + record.TotalPrinting + record.TotalCuring + record.TotalChecking + record.TotalPacking + record.TotalDispatch;

                if (previousTotal + batchOthersTotal + currentTotal > productionRecord.IssueQty)
                    return BadRequest($"Total pieces distributed across all stages for {record.StyleNo} ({previousTotal + batchOthersTotal + currentTotal}) exceeds production issue qty ({productionRecord.IssueQty}).");

                _context.DailyOutputRecords.Add(record);
                saved.Add(record);
            }

            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Create", "DailyOutput", string.Join(",", saved.Select(r => r.Id)),
                $"Batch logged {saved.Count} daily output(s) for {saved.FirstOrDefault()?.StyleNo}");

            return Ok(saved);
        }

        [HttpPut("daily-output/{id}")]
        public async Task<ActionResult<DailyOutputRecord>> UpdateDailyOutput(string id, DailyOutputRecord record)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Record ID is required.");

            var existing = await _context.DailyOutputRecords.FirstOrDefaultAsync(r => r.Id == id);
            if (existing == null) return NotFound("Daily output record not found.");

            var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);
            if (storeIn == null) return BadRequest("Linked Store-In record not found.");

            var productionRecord = await _context.StoreProductionRecords.FirstOrDefaultAsync(p => p.Id == existing.ProductionRecordId);
            if (productionRecord == null) return BadRequest("Production record not found.");

            existing.Date = record.Date;
            existing.Component = record.Component;
            existing.TableNo = record.TableNo;
            existing.Target = record.Target;
            existing.DailyTarget = record.DailyTarget;
            existing.TimeSlots = record.TimeSlots ?? new List<TimeSlotEntry>();
            existing.WorkerName = record.WorkerName;

            existing.TotalSeating = existing.TimeSlots.Sum(t => t.Seating);
            existing.TotalPrinting = existing.TimeSlots.Sum(t => t.Printing);
            existing.TotalCuring = existing.TimeSlots.Sum(t => t.Curing);
            existing.TotalChecking = existing.TimeSlots.Sum(t => t.Checking);
            existing.TotalPacking = existing.TimeSlots.Sum(t => t.Packing);
            existing.TotalDispatch = existing.TimeSlots.Sum(t => t.Dispatch);

            // FIXED: Validate stages cumulatively against IssueQty
            var otherOutputs = await _context.DailyOutputRecords
                .Where(d => d.ProductionRecordId == existing.ProductionRecordId && d.Id != existing.Id)
                .Select(d => new { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch })
                .ToListAsync();

            var otherTotal = otherOutputs.Sum(x => x.TotalSeating + x.TotalPrinting + x.TotalCuring + x.TotalChecking + x.TotalPacking + x.TotalDispatch);
            var currentTotal = existing.TotalSeating + existing.TotalPrinting + existing.TotalCuring + existing.TotalChecking + existing.TotalPacking + existing.TotalDispatch;

            if (otherTotal + currentTotal > productionRecord.IssueQty)
                return BadRequest($"Total pieces distributed across all stages ({otherTotal + currentTotal}) exceeds production issue qty ({productionRecord.IssueQty}).");

            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Update", "DailyOutput", existing.Id,
                $"Updated daily output for {existing.StyleNo} (Cut: {existing.CutNo})");

            return Ok(existing);
        }

        [HttpDelete("daily-output/{id}")]
        public async Task<IActionResult> DeleteDailyOutput(string id)
        {
            var record = await _context.DailyOutputRecords.FindAsync(id);
            if (record == null) return NotFound();

            _context.DailyOutputRecords.Remove(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "DailyOutput", id,
                $"Deleted daily output for {record.StyleNo}, Table: {record.TableNo}");

            return NoContent();
        }

        // ==========================================
        // DOWNTIME REPORTS
        // ==========================================

        [HttpGet("downtime")]
        public async Task<ActionResult<IEnumerable<DowntimeRecord>>> GetDowntimeRecords()
        {
            return await _context.DowntimeRecords
                .OrderByDescending(r => r.Date)
                .ToListAsync();
        }

        [HttpPost("downtime")]
        public async Task<ActionResult<DowntimeRecord>> CreateDowntimeRecord(DowntimeRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.Date))
                return BadRequest("Date is required.");

            if (record.Entries == null || record.Entries.Count == 0)
                return BadRequest("At least one downtime entry is required.");

            foreach (var entry in record.Entries)
            {
                if (entry.Hours <= 0)
                    return BadRequest($"Hours for '{entry.Type}' must be > 0.");
                if (string.IsNullOrWhiteSpace(entry.Reason))
                    return BadRequest($"Reason for '{entry.Type}' is required.");

                entry.AcknowledgedBy = string.Empty;
                entry.IsAcknowledged = false;
            }

            if (string.IsNullOrWhiteSpace(record.Id))
                record.Id = Guid.NewGuid().ToString();

            if (!string.IsNullOrWhiteSpace(record.StoreInRecordId))
            {
                var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);
                if (storeIn != null)
                {
                    record.SubmissionId = storeIn.SubmissionId;
                    record.StyleNo = storeIn.StyleNo ?? string.Empty;
                    record.CustomerName = storeIn.CustomerName ?? string.Empty;
                }
            }

            record.TotalHours = record.Entries.Sum(e => e.Hours);
            record.FullyAcknowledged = false;

            _context.DowntimeRecords.Add(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "Downtime", record.Id,
                $"Submitted {record.TotalHours} hrs downtime — {record.WorkerName} ({record.StyleNo})");

            return CreatedAtAction(nameof(GetDowntimeRecords), new { id = record.Id }, record);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("downtime/{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeDowntime(string id, [FromBody] AcknowledgeRequest request)
        {
            var record = await _context.DowntimeRecords.FindAsync(id);
            if (record == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(request.AcknowledgedBy))
                return BadRequest("AcknowledgedBy name is required.");

            foreach (var entry in record.Entries)
            {
                entry.IsAcknowledged = true;
                entry.AcknowledgedBy = request.AcknowledgedBy;
            }

            record.FullyAcknowledged = true;
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Update", "Downtime", id,
                $"Approved downtime for {record.WorkerName} ({record.TotalHours} hrs)");

            return Ok(record);
        }

        [HttpDelete("downtime/{id}")]
        public async Task<IActionResult> DeleteDowntimeRecord(string id)
        {
            var record = await _context.DowntimeRecords.FindAsync(id);
            if (record == null) return NotFound();

            _context.DowntimeRecords.Remove(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "Downtime", id,
                $"Deleted downtime for {record.WorkerName} ({record.TotalHours} hrs)");

            return NoContent();
        }
    }

    public class AcknowledgeRequest
    {
        public string AcknowledgedBy { get; set; } = string.Empty;
    }
}