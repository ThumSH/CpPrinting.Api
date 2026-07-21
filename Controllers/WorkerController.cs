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

        // ── Stage allocation helper ───────────────────────────────────────────
        // Each production stage has its own independent allocation limit.
        // Example: IssueQty 21 means Seating can total 21, Printing can total 21,
        // Curing can total 21, etc. The stages must NOT be summed together.
        private static string? ValidateIndependentStageLimit(
            int issueQty,
            int previousSeating, int previousPrinting, int previousCuring,
            int previousChecking, int previousPacking, int previousDispatch,
            int currentSeating, int currentPrinting, int currentCuring,
            int currentChecking, int currentPacking, int currentDispatch)
        {
            var checks = new[]
            {
                new { Stage = "Seating",  Total = previousSeating  + currentSeating  },
                new { Stage = "Printing", Total = previousPrinting + currentPrinting },
                new { Stage = "Curing",   Total = previousCuring   + currentCuring   },
                new { Stage = "Checking", Total = previousChecking + currentChecking },
                new { Stage = "Packing",  Total = previousPacking  + currentPacking  },
                new { Stage = "Dispatch", Total = previousDispatch + currentDispatch },
            };

            var exceeded = checks.FirstOrDefault(x => x.Total > issueQty);
            if (exceeded == null) return null;

            return $"{exceeded.Stage} allocation ({exceeded.Total}) exceeds production issue qty ({issueQty}). " +
                   $"Each stage has its own independent limit of {issueQty}.";
        }

        private async Task<bool> IsProductionRecordManuallyCompleted(string productionRecordId)
        {
            if (string.IsNullOrWhiteSpace(productionRecordId)) return false;

            return await _context.DailyOutputRecords.AnyAsync(d =>
                d.ProductionRecordId == productionRecordId &&
                d.IsJobCompleted);
        }


        [HttpGet("eligible-styles")]
        public async Task<ActionResult> GetEligibleStyles(
            [FromQuery] string? productionRecordId = null)
        {
            var productionQuery = _context.StoreProductionRecords
                .AsNoTracking()
                .AsQueryable();

            // Continue supplies the exact StoreProductionRecord primary key.
            // Normal dropdown loading omits it and keeps the existing behaviour.
            if (!string.IsNullOrWhiteSpace(productionRecordId))
            {
                var exactId = productionRecordId.Trim();
                productionQuery = productionQuery.Where(p => p.Id == exactId);
            }

            var productionRecords = await productionQuery
                .OrderByDescending(p => p.IssueDate)
                .ToListAsync();

            if (!productionRecords.Any())
                return Ok(Array.Empty<object>());

            var requestedProductionIds = productionRecords
                .Select(p => p.Id)
                .ToList();

            var completedProductionIds = await _context.DailyOutputRecords
                .AsNoTracking()
                .Where(d =>
                    d.IsJobCompleted &&
                    !string.IsNullOrWhiteSpace(d.ProductionRecordId) &&
                    requestedProductionIds.Contains(d.ProductionRecordId))
                .Select(d => d.ProductionRecordId)
                .Distinct()
                .ToListAsync();

            if (completedProductionIds.Any())
            {
                productionRecords = productionRecords
                    .Where(p => !completedProductionIds.Contains(p.Id))
                    .ToList();
            }

            if (!productionRecords.Any())
                return Ok(Array.Empty<object>());

            var storeInIds = productionRecords.Select(p => p.StoreInRecordId).Distinct().ToList();
            var storeInRecords = await _context.StoreInRecords
                .AsNoTracking()
                .Where(s => storeInIds.Contains(s.Id))
                .ToListAsync();
            var storeInMap = storeInRecords.ToDictionary(s => s.Id);

            var cpiReports = await _context.CpiReports
                .AsNoTracking()
                .Where(r => storeInIds.Contains(r.StoreInRecordId))
                .ToListAsync();

            // A Store-In can have more than one CPI report in real data. A
            // lookup prevents duplicate-key exceptions that would make
            // /eligible-styles fail and break Continue.
            var cpiByStoreIn = cpiReports
                .Where(r => !string.IsNullOrWhiteSpace(r.StoreInRecordId))
                .ToLookup(r => r.StoreInRecordId);

            var remainingProductionIds = productionRecords
                .Select(p => p.Id)
                .ToList();

            var dailyOutputs = await _context.DailyOutputRecords
                .AsNoTracking()
                .Where(d =>
                    !d.IsJobCompleted &&
                    !string.IsNullOrWhiteSpace(d.ProductionRecordId) &&
                    remainingProductionIds.Contains(d.ProductionRecordId))
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

            // Each stage is allocated independently. Do NOT sum Seating + Printing +
            // Curing etc. against IssueQty. IssueQty is the limit for EACH stage.
            var stageTotalsByProduction = dailyOutputs
                .GroupBy(d => d.ProductionRecordId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Seating  = g.Sum(x => x.TotalSeating),
                        Printing = g.Sum(x => x.TotalPrinting),
                        Curing   = g.Sum(x => x.TotalCuring),
                        Checking = g.Sum(x => x.TotalChecking),
                        Packing  = g.Sum(x => x.TotalPacking),
                        Dispatch = g.Sum(x => x.TotalDispatch)
                    }
                );

            var result = productionRecords.Select(p =>
            {
                storeInMap.TryGetValue(p.StoreInRecordId, out var storeIn);
                stageTotalsByProduction.TryGetValue(p.Id, out var stageTotals);

                var seatingAllocated  = stageTotals?.Seating  ?? 0;
                var printingAllocated = stageTotals?.Printing ?? 0;
                var curingAllocated   = stageTotals?.Curing   ?? 0;
                var checkingAllocated = stageTotals?.Checking ?? 0;
                var packingAllocated  = stageTotals?.Packing  ?? 0;
                var dispatchAllocated = stageTotals?.Dispatch ?? 0;

                var seatingRemaining  = Math.Max(0, p.IssueQty - seatingAllocated);
                var printingRemaining = Math.Max(0, p.IssueQty - printingAllocated);
                var curingRemaining   = Math.Max(0, p.IssueQty - curingAllocated);
                var checkingRemaining = Math.Max(0, p.IssueQty - checkingAllocated);
                var packingRemaining  = Math.Max(0, p.IssueQty - packingAllocated);
                var dispatchRemaining = Math.Max(0, p.IssueQty - dispatchAllocated);

                // Backward-compatible field used by old UI/dropdowns:
                // keep the production row visible while at least one stage still has qty left.
                var maxRemainingQty = new[]
                {
                    seatingRemaining, printingRemaining, curingRemaining,
                    checkingRemaining, packingRemaining, dispatchRemaining
                }.Max();

                var maxAllocatedQty = new[]
                {
                    seatingAllocated, printingAllocated, curingAllocated,
                    checkingAllocated, packingAllocated, dispatchAllocated
                }.Max();

                var resolvedComponent = p.Components;
                if (string.IsNullOrWhiteSpace(resolvedComponent))
                {
                    var cpiCut = cpiByStoreIn[p.StoreInRecordId]
                        .SelectMany(report => report.CutInspections ?? new())
                        .FirstOrDefault(ci => ci.CutNo == p.CutNo);

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
                    DispatchedQty = maxAllocatedQty,
                    OrderQty = maxRemainingQty,

                    SeatingAllocated = seatingAllocated,
                    PrintingAllocated = printingAllocated,
                    CuringAllocated = curingAllocated,
                    CheckingAllocated = checkingAllocated,
                    PackingAllocated = packingAllocated,
                    DispatchAllocated = dispatchAllocated,

                    SeatingRemaining = seatingRemaining,
                    PrintingRemaining = printingRemaining,
                    CuringRemaining = curingRemaining,
                    CheckingRemaining = checkingRemaining,
                    PackingRemaining = packingRemaining,
                    DispatchRemaining = dispatchRemaining,
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
            [FromQuery] string? productionRecordId = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null)
        {
            var query = _context.DailyOutputRecords
                .AsNoTracking()
                .Where(r => !r.IsJobCompleted)
                .AsQueryable();

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
            if (!string.IsNullOrWhiteSpace(productionRecordId))
            {
                var exactProductionId = productionRecordId.Trim();
                query = query.Where(r => r.ProductionRecordId == exactProductionId);
            }

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


        // Exact History Continue payload. This single request returns both the
        // clicked Daily Output row and its linked Production allocation summary.
        // It avoids two independent requests and prevents the Worker page from
        // rendering before both records are available.
        [HttpGet("daily-output/{id}/resume")]
        public async Task<ActionResult> GetDailyOutputResume(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Daily Output record ID is required.");

            var exactId = id.Trim();

            var record = await _context.DailyOutputRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Id == exactId &&
                    !r.IsJobCompleted);

            if (record == null)
                return NotFound("Daily Output record was not found.");

            if (string.IsNullOrWhiteSpace(record.ProductionRecordId))
                return BadRequest("This Daily Output record is missing its ProductionRecordId.");

            var productionRecord = await _context.StoreProductionRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == record.ProductionRecordId);

            if (productionRecord == null)
                return NotFound("The linked Production record was not found.");

            var manuallyCompleted = await _context.DailyOutputRecords
                .AsNoTracking()
                .AnyAsync(d =>
                    d.ProductionRecordId == productionRecord.Id &&
                    d.IsJobCompleted);

            if (manuallyCompleted)
                return Conflict("This production job was manually completed and cannot be continued.");

            var storeIn = await _context.StoreInRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == productionRecord.StoreInRecordId);

            var outputRows = await _context.DailyOutputRecords
                .AsNoTracking()
                .Where(d =>
                    !d.IsJobCompleted &&
                    d.ProductionRecordId == productionRecord.Id)
                .Select(d => new
                {
                    d.TotalSeating,
                    d.TotalPrinting,
                    d.TotalCuring,
                    d.TotalChecking,
                    d.TotalPacking,
                    d.TotalDispatch
                })
                .ToListAsync();

            var seatingAllocated = outputRows.Sum(x => x.TotalSeating);
            var printingAllocated = outputRows.Sum(x => x.TotalPrinting);
            var curingAllocated = outputRows.Sum(x => x.TotalCuring);
            var checkingAllocated = outputRows.Sum(x => x.TotalChecking);
            var packingAllocated = outputRows.Sum(x => x.TotalPacking);
            var dispatchAllocated = outputRows.Sum(x => x.TotalDispatch);

            var seatingRemaining = Math.Max(0, productionRecord.IssueQty - seatingAllocated);
            var printingRemaining = Math.Max(0, productionRecord.IssueQty - printingAllocated);
            var curingRemaining = Math.Max(0, productionRecord.IssueQty - curingAllocated);
            var checkingRemaining = Math.Max(0, productionRecord.IssueQty - checkingAllocated);
            var packingRemaining = Math.Max(0, productionRecord.IssueQty - packingAllocated);
            var dispatchRemaining = Math.Max(0, productionRecord.IssueQty - dispatchAllocated);

            var remainingValues = new[]
            {
                seatingRemaining,
                printingRemaining,
                curingRemaining,
                checkingRemaining,
                packingRemaining,
                dispatchRemaining
            };

            var allocatedValues = new[]
            {
                seatingAllocated,
                printingAllocated,
                curingAllocated,
                checkingAllocated,
                packingAllocated,
                dispatchAllocated
            };

            var maxRemainingQty = remainingValues.Max();
            var maxAllocatedQty = allocatedValues.Max();

            var resolvedComponent =
                !string.IsNullOrWhiteSpace(productionRecord.Components)
                    ? productionRecord.Components
                    : !string.IsNullOrWhiteSpace(record.Component)
                        ? record.Component
                        : storeIn?.Components ?? string.Empty;

            record.TimeSlots ??= new List<TimeSlotEntry>();

            var eligibleStyle = new
            {
                Id = productionRecord.Id,
                ProductionRecordId = productionRecord.Id,
                StoreInRecordId = productionRecord.StoreInRecordId,
                SubmissionId = productionRecord.SubmissionId ?? storeIn?.SubmissionId ?? string.Empty,
                StyleNo = productionRecord.StyleNo ?? record.StyleNo ?? string.Empty,
                CustomerName = productionRecord.CustomerName ?? record.CustomerName ?? string.Empty,
                ScheduleNo = storeIn?.ScheduleNo ?? string.Empty,
                Components = resolvedComponent,
                Component = resolvedComponent,
                BodyColour = storeIn?.BodyColour ?? string.Empty,
                CutNo = productionRecord.CutNo ?? record.CutNo ?? string.Empty,
                LineNo = productionRecord.LineNo ?? string.Empty,
                IssueDate = productionRecord.IssueDate ?? string.Empty,
                OriginalQty = productionRecord.IssueQty,
                DispatchedQty = maxAllocatedQty,
                OrderQty = maxRemainingQty,

                SeatingAllocated = seatingAllocated,
                PrintingAllocated = printingAllocated,
                CuringAllocated = curingAllocated,
                CheckingAllocated = checkingAllocated,
                PackingAllocated = packingAllocated,
                DispatchAllocated = dispatchAllocated,

                SeatingRemaining = seatingRemaining,
                PrintingRemaining = printingRemaining,
                CuringRemaining = curingRemaining,
                CheckingRemaining = checkingRemaining,
                PackingRemaining = packingRemaining,
                DispatchRemaining = dispatchRemaining,
            };

            return Ok(new
            {
                Record = record,
                EligibleStyle = eligibleStyle,
                CanContinue = maxRemainingQty > 0,
                Message = maxRemainingQty > 0
                    ? string.Empty
                    : "All production stages have reached the issued quantity."
            });
        }

        // Exact History Continue lookup. DailyOutputRecord.Id is the primary
        // key, so SQL Server can retrieve the clicked row directly.
        [HttpGet("daily-output/{id}")]
        public async Task<ActionResult<DailyOutputRecord>> GetDailyOutputRecordById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Daily Output record ID is required.");

            var record = await _context.DailyOutputRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsJobCompleted);

            if (record == null)
                return NotFound("Daily Output record was not found.");

            return Ok(record);
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

            // Validate each process stage independently against IssueQty.
            // Seating, Printing, Curing, etc. each get their own full IssueQty limit.
            if (await IsProductionRecordManuallyCompleted(record.ProductionRecordId))
                return BadRequest("This job has already been manually completed.");

            var existingOutputs = await _context.DailyOutputRecords
                .Where(d => !d.IsJobCompleted && d.ProductionRecordId == record.ProductionRecordId)
                .Select(d => new { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch })
                .ToListAsync();

            var validationError = ValidateIndependentStageLimit(
                productionRecord.IssueQty,
                existingOutputs.Sum(x => x.TotalSeating),
                existingOutputs.Sum(x => x.TotalPrinting),
                existingOutputs.Sum(x => x.TotalCuring),
                existingOutputs.Sum(x => x.TotalChecking),
                existingOutputs.Sum(x => x.TotalPacking),
                existingOutputs.Sum(x => x.TotalDispatch),
                record.TotalSeating,
                record.TotalPrinting,
                record.TotalCuring,
                record.TotalChecking,
                record.TotalPacking,
                record.TotalDispatch
            );

            if (validationError != null)
                return BadRequest(validationError);

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

                if (await IsProductionRecordManuallyCompleted(record.ProductionRecordId))
                    return BadRequest($"Production record '{record.ProductionRecordId}' has already been manually completed.");

                // Validate each process stage independently against DB records AND this batch.
                var existingOutputs = await _context.DailyOutputRecords
                    .Where(d => !d.IsJobCompleted && d.ProductionRecordId == record.ProductionRecordId)
                    .Select(d => new { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch })
                    .ToListAsync();

                var inBatchOutputs = records
                    .Where(d => d.ProductionRecordId == record.ProductionRecordId && d != record)
                    .Select(d => new
                    {
                        TotalSeating = d.TimeSlots?.Sum(t => t.Seating) ?? 0,
                        TotalPrinting = d.TimeSlots?.Sum(t => t.Printing) ?? 0,
                        TotalCuring = d.TimeSlots?.Sum(t => t.Curing) ?? 0,
                        TotalChecking = d.TimeSlots?.Sum(t => t.Checking) ?? 0,
                        TotalPacking = d.TimeSlots?.Sum(t => t.Packing) ?? 0,
                        TotalDispatch = d.TimeSlots?.Sum(t => t.Dispatch) ?? 0
                    })
                    .ToList();

                var validationError = ValidateIndependentStageLimit(
                    productionRecord.IssueQty,
                    existingOutputs.Sum(x => x.TotalSeating) + inBatchOutputs.Sum(x => x.TotalSeating),
                    existingOutputs.Sum(x => x.TotalPrinting) + inBatchOutputs.Sum(x => x.TotalPrinting),
                    existingOutputs.Sum(x => x.TotalCuring) + inBatchOutputs.Sum(x => x.TotalCuring),
                    existingOutputs.Sum(x => x.TotalChecking) + inBatchOutputs.Sum(x => x.TotalChecking),
                    existingOutputs.Sum(x => x.TotalPacking) + inBatchOutputs.Sum(x => x.TotalPacking),
                    existingOutputs.Sum(x => x.TotalDispatch) + inBatchOutputs.Sum(x => x.TotalDispatch),
                    record.TotalSeating,
                    record.TotalPrinting,
                    record.TotalCuring,
                    record.TotalChecking,
                    record.TotalPacking,
                    record.TotalDispatch
                );

                if (validationError != null)
                    return BadRequest(validationError);

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
            if (existing == null || existing.IsJobCompleted) return NotFound("Daily output record not found.");

            if (await IsProductionRecordManuallyCompleted(existing.ProductionRecordId))
                return BadRequest("This job has already been manually completed.");

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

            // Validate each process stage independently against IssueQty.
            var otherOutputs = await _context.DailyOutputRecords
                .Where(d => !d.IsJobCompleted && d.ProductionRecordId == existing.ProductionRecordId && d.Id != existing.Id)
                .Select(d => new { d.TotalSeating, d.TotalPrinting, d.TotalCuring, d.TotalChecking, d.TotalPacking, d.TotalDispatch })
                .ToListAsync();

            var validationError = ValidateIndependentStageLimit(
                productionRecord.IssueQty,
                otherOutputs.Sum(x => x.TotalSeating),
                otherOutputs.Sum(x => x.TotalPrinting),
                otherOutputs.Sum(x => x.TotalCuring),
                otherOutputs.Sum(x => x.TotalChecking),
                otherOutputs.Sum(x => x.TotalPacking),
                otherOutputs.Sum(x => x.TotalDispatch),
                existing.TotalSeating,
                existing.TotalPrinting,
                existing.TotalCuring,
                existing.TotalChecking,
                existing.TotalPacking,
                existing.TotalDispatch
            );

            if (validationError != null)
                return BadRequest(validationError);

            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Update", "DailyOutput", existing.Id,
                $"Updated daily output for {existing.StyleNo} (Cut: {existing.CutNo})");

            return Ok(existing);
        }

        [HttpDelete("daily-output/{id}")]
        public async Task<IActionResult> DeleteDailyOutput(string id)
        {
            var record = await _context.DailyOutputRecords.FindAsync(id);
            if (record == null || record.IsJobCompleted) return NotFound();

            _context.DailyOutputRecords.Remove(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "DailyOutput", id,
                $"Deleted daily output for {record.StyleNo}, Table: {record.TableNo}");

            return NoContent();
        }

        [HttpPost("complete-job/{productionRecordId}")]
        public async Task<IActionResult> CompleteJob(string productionRecordId)
        {
            if (string.IsNullOrWhiteSpace(productionRecordId))
                return BadRequest("ProductionRecordId is required.");

            var productionRecord = await _context.StoreProductionRecords
                .FirstOrDefaultAsync(p => p.Id == productionRecordId);

            if (productionRecord == null)
                return NotFound("Production record not found.");

            if (await IsProductionRecordManuallyCompleted(productionRecordId))
            {
                return Ok(new
                {
                    ProductionRecordId = productionRecordId,
                    Completed = true,
                    Message = "Job is already completed."
                });
            }

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(s => s.Id == productionRecord.StoreInRecordId);

            var completedAt = DateTime.UtcNow;
            var completedBy = User?.Identity?.Name ?? string.Empty;

            var marker = new DailyOutputRecord
            {
                Id = Guid.NewGuid().ToString(),
                StoreInRecordId = productionRecord.StoreInRecordId ?? string.Empty,
                ProductionRecordId = productionRecord.Id,
                SubmissionId = productionRecord.SubmissionId ?? storeIn?.SubmissionId ?? string.Empty,
                Date = completedAt.ToString("yyyy-MM-dd"),
                StyleNo = productionRecord.StyleNo ?? storeIn?.StyleNo ?? string.Empty,
                CustomerName = productionRecord.CustomerName ?? storeIn?.CustomerName ?? string.Empty,
                CutNo = productionRecord.CutNo ?? string.Empty,
                Component = productionRecord.Components ?? string.Empty,
                OrderQty = productionRecord.IssueQty,
                TableNo = "COMPLETED",
                WorkerName = completedBy,
                IsJobCompleted = true,
                CompletedAt = completedAt.ToString("O"),
                CompletedBy = completedBy,
                TimeSlots = new List<TimeSlotEntry>(),
                TotalSeating = 0,
                TotalPrinting = 0,
                TotalCuring = 0,
                TotalChecking = 0,
                TotalPacking = 0,
                TotalDispatch = 0
            };

            _context.DailyOutputRecords.Add(marker);
            await _context.SaveChangesAsync();

            await _logger.Log(User!, HttpContext, "Complete", "WorkerJob", productionRecord.Id,
                $"Manually completed worker job for {marker.StyleNo} (Cut: {marker.CutNo}, Component: {marker.Component})");

            return Ok(new
            {
                ProductionRecordId = productionRecord.Id,
                Completed = true,
                Message = "Job completed successfully."
            });
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