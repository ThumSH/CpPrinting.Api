using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        /// <summary>
        /// Returns styles that have been issued to production.
        /// OrderQty = IssueQty from the production record (NOT the bulk qty).
        /// Each production record is a separate selectable item for the worker.
        /// </summary>
        [HttpGet("eligible-styles")]
        public async Task<ActionResult> GetEligibleStyles()
        {
            var productionRecords = await _context.StoreProductionRecords
                .OrderByDescending(p => p.IssueDate)
                .ToListAsync();

            if (!productionRecords.Any())
                return Ok(Array.Empty<object>());

            // Get store-in records for schedule/colour info
            var storeInIds = productionRecords.Select(p => p.StoreInRecordId).Distinct().ToList();
            var storeInRecords = await _context.StoreInRecords
                .Where(s => storeInIds.Contains(s.Id))
                .ToListAsync();

            var storeInMap = storeInRecords.ToDictionary(s => s.Id);

            var result = productionRecords.Select(p =>
            {
                storeInMap.TryGetValue(p.StoreInRecordId, out var storeIn);

                return new
                {
                    p.Id,                                          // Production record ID (used as selection key)
                    StoreInRecordId = p.StoreInRecordId,           // For linking DailyOutputRecord
                    p.SubmissionId,
                    StyleNo = p.StyleNo ?? string.Empty,
                    CustomerName = p.CustomerName ?? string.Empty,
                    ScheduleNo = storeIn?.ScheduleNo ?? string.Empty,
                    Components = p.Components ?? storeIn?.Components ?? string.Empty,
                    BodyColour = storeIn?.BodyColour ?? string.Empty,
                    CutNo = p.CutNo ?? string.Empty,
                    LineNo = p.LineNo ?? string.Empty,
                    IssueDate = p.IssueDate ?? string.Empty,
                    OrderQty = p.IssueQty,                         // IssueQty IS the order qty for workers
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("daily-output")]
        public async Task<ActionResult<IEnumerable<DailyOutputRecord>>> GetDailyOutputRecords()
        {
            return await _context.DailyOutputRecords
                .OrderByDescending(r => r.Date)
                .ToListAsync();
        }

        [HttpPost("daily-output")]
        public async Task<ActionResult<DailyOutputRecord>> CreateDailyOutput(DailyOutputRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(record.TableNo))
                return BadRequest("TableNo is required.");

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            // Validate that production records exist for this store-in
            var productionRecords = await _context.StoreProductionRecords
                .Where(p => p.StoreInRecordId == record.StoreInRecordId)
                .ToListAsync();

            if (!productionRecords.Any())
                return BadRequest("No production records found for this Store-In. Items must be issued to production before worker output can be logged.");

            if (string.IsNullOrWhiteSpace(record.Id))
                record.Id = Guid.NewGuid().ToString();

            // Backend source of truth
            record.SubmissionId = storeIn.SubmissionId;
            record.StyleNo = storeIn.StyleNo ?? string.Empty;
            record.CustomerName = storeIn.CustomerName ?? string.Empty;

            // OrderQty from production if not provided
            if (record.OrderQty <= 0)
                record.OrderQty = productionRecords.Sum(p => p.IssueQty);

            // Calculate totals from time slots
            record.TotalSeating = record.TimeSlots?.Sum(t => t.Seating) ?? 0;
            record.TotalPrinting = record.TimeSlots?.Sum(t => t.Printing) ?? 0;
            record.TotalCuring = record.TimeSlots?.Sum(t => t.Curing) ?? 0;
            record.TotalChecking = record.TimeSlots?.Sum(t => t.Checking) ?? 0;
            record.TotalPacking = record.TimeSlots?.Sum(t => t.Packing) ?? 0;
            record.TotalDispatch = record.TimeSlots?.Sum(t => t.Dispatch) ?? 0;

            _context.DailyOutputRecords.Add(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "DailyOutput", record.Id,
                $"Logged daily output for {record.StyleNo}, Table: {record.TableNo}");

            return CreatedAtAction(nameof(GetDailyOutputRecords), new { id = record.Id }, record);
        }

        [HttpPost("daily-output/batch")]
        public async Task<ActionResult<IEnumerable<DailyOutputRecord>>> BatchCreateDailyOutput(
            [FromBody] List<DailyOutputRecord> records)
        {
            if (records == null || records.Count == 0)
                return BadRequest("At least one record is required.");

            var saved = new List<DailyOutputRecord>();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                    return BadRequest("StoreInRecordId is required.");

                var storeIn = await _context.StoreInRecords
                    .FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);

                if (storeIn == null)
                    return BadRequest("Linked Store-In record not found.");

                // Validate production exists
                var productionRecords = await _context.StoreProductionRecords
                    .Where(p => p.StoreInRecordId == record.StoreInRecordId)
                    .ToListAsync();

                if (!productionRecords.Any())
                    return BadRequest($"No production records for Store-In '{storeIn.StyleNo}'. Items must be issued to production first.");

                record.Id = Guid.NewGuid().ToString();
                record.SubmissionId = storeIn.SubmissionId;
                record.StyleNo = storeIn.StyleNo ?? string.Empty;
                record.CustomerName = storeIn.CustomerName ?? string.Empty;

                // Set OrderQty from production if not provided
                if (record.OrderQty <= 0)
                    record.OrderQty = productionRecords.Sum(p => p.IssueQty);

                record.TotalSeating = record.TimeSlots?.Sum(t => t.Seating) ?? 0;
                record.TotalPrinting = record.TimeSlots?.Sum(t => t.Printing) ?? 0;
                record.TotalCuring = record.TimeSlots?.Sum(t => t.Curing) ?? 0;
                record.TotalChecking = record.TimeSlots?.Sum(t => t.Checking) ?? 0;
                record.TotalPacking = record.TimeSlots?.Sum(t => t.Packing) ?? 0;
                record.TotalDispatch = record.TimeSlots?.Sum(t => t.Dispatch) ?? 0;

                _context.DailyOutputRecords.Add(record);
                saved.Add(record);
            }

            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "DailyOutput", string.Join(",", saved.Select(r => r.Id)),
                $"Batch logged {saved.Count} daily output(s) for {saved.FirstOrDefault()?.StyleNo}");

            return Ok(saved);
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

            // Validate each entry has hours > 0 and a reason
            foreach (var entry in record.Entries)
            {
                if (entry.Hours <= 0)
                    return BadRequest($"Hours for '{entry.Type}' must be > 0.");
                if (string.IsNullOrWhiteSpace(entry.Reason))
                    return BadRequest($"Reason for '{entry.Type}' is required.");

                // Reset acknowledgement — worker cannot self-acknowledge
                entry.AcknowledgedBy = string.Empty;
                entry.IsAcknowledged = false;
            }

            if (string.IsNullOrWhiteSpace(record.Id))
                record.Id = Guid.NewGuid().ToString();

            if (!string.IsNullOrWhiteSpace(record.StoreInRecordId))
            {
                var storeIn = await _context.StoreInRecords
                    .FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);

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

        /// <summary>
        /// Admin-only endpoint to acknowledge downtime entries.
        /// </summary>
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