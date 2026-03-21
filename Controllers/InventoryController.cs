using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.DTOs;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Stores,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InventoryController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // HELPER: Get total IN qty already used for a submission
        // ==========================================
        private async Task<int> GetTotalInQtyForSubmission(string submissionId, string? excludeStoreInId = null)
        {
            var query = _context.StoreInRecords
                .Where(r => r.SubmissionId == submissionId);

            if (!string.IsNullOrEmpty(excludeStoreInId))
                query = query.Where(r => r.Id != excludeStoreInId);

            return await query.SumAsync(r => r.InQty);
        }

        // ==========================================
        // HELPER: Map StoreInRecord entity to response DTO
        // ==========================================
        private static StoreInResponseDto MapToResponse(StoreInRecord record)
        {
            return new StoreInResponseDto
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
                ScheduleNo = record.ScheduleNo,
                CutInDate = record.CutInDate ?? string.Empty,
                BulkQty = record.BulkQty,
                InQty = record.InQty,
                BalanceBulkQty = record.BalanceBulkQty,
                TotalCutQty = record.TotalCutQty,
                UncutBalance = record.UncutBalance,
                AvailableQty = record.AvailableQty,
                Cuts = record.Cuts.Select(c => new CutResponseDto
                {
                    Id = c.Id,
                    CutNo = c.CutNo,
                    CutQty = c.CutQty,
                    Bundles = c.Bundles.Select(b => new BundleResponseDto
                    {
                        Id = b.Id,
                        BundleNo = b.BundleNo,
                        BundleQty = b.BundleQty,
                        Size = b.Size,
                        NumberRange = b.NumberRange ?? string.Empty
                    }).ToList()
                }).ToList()
            };
        }

        // ==========================================
        // ELIGIBLE STYLES FOR STORE IN
        // Now includes RemainingBulkQty (global balance)
        // ==========================================
        [HttpGet("eligible-store-in")]
        public async Task<ActionResult<IEnumerable<EligibleStoreInDto>>> GetEligibleStoreInStyles()
        {
            // Get all approved latest-revision submissions with their job info
            var rawEligibleStyles = await (
                from submission in _context.Submissions
                join approval in _context.Approvals
                    on submission.Id equals approval.SubmissionId
                join job in _context.DevelopmentJobs
                    on new
                    {
                        StyleNo = submission.StyleNo.ToLower(),
                        Customer = submission.CustomerName.ToLower()
                    }
                    equals new
                    {
                        StyleNo = job.StyleNo.ToLower(),
                        Customer = job.Customer.ToLower()
                    }
                where submission.IsLatestRevision == true
                      && approval.Status == "Approved"
                orderby approval.ReviewedAt descending
                select new
                {
                    submission.Id,
                    submission.RevisionNo,
                    submission.StyleNo,
                    submission.CustomerName,
                    submission.SubmissionDate,
                    submission.Level,
                    ApprovalStatus = approval.Status,
                    approval.ReviewedAt,
                    job.BodyColour,
                    job.PrintColour,
                    job.Season,
                    Components = string.Join(", ", job.Placements),
                    approval.BulkOrderQty
                }
            ).ToListAsync();

            // Get total IN qty per submission for bulk balance
            var inQtyBySubmission = await _context.StoreInRecords
                .GroupBy(r => r.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, TotalInQty = g.Sum(r => r.InQty) })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.TotalInQty);

            var eligibleStyles = rawEligibleStyles.Select(x =>
            {
                var approvedBulk = int.TryParse(x.BulkOrderQty, out var qty) ? qty : 0;
                var totalUsed = inQtyBySubmission.GetValueOrDefault(x.Id, 0);
                var remaining = Math.Max(0, approvedBulk - totalUsed);

                return new EligibleStoreInDto
                {
                    SubmissionId = x.Id,
                    RevisionNo = x.RevisionNo,
                    StyleNo = x.StyleNo,
                    CustomerName = x.CustomerName,
                    SubmissionDate = x.SubmissionDate,
                    Level = x.Level,
                    ApprovalStatus = x.ApprovalStatus,
                    ReviewedAt = x.ReviewedAt,
                    BodyColour = x.BodyColour,
                    PrintColour = x.PrintColour,
                    Season = x.Season,
                    Components = x.Components,
                    ApprovedBulkQty = approvedBulk,
                    RemainingBulkQty = remaining
                };
            }).ToList();

            return Ok(eligibleStyles);
        }

        // ==========================================
        // BULK BALANCE — global per-style summary
        // ==========================================
        [HttpGet("bulk-balance")]
        public async Task<ActionResult<IEnumerable<BulkBalanceDto>>> GetBulkBalances()
        {
            var approvals = await (
                from submission in _context.Submissions
                join approval in _context.Approvals
                    on submission.Id equals approval.SubmissionId
                where submission.IsLatestRevision == true
                      && approval.Status == "Approved"
                select new
                {
                    submission.Id,
                    submission.StyleNo,
                    submission.CustomerName,
                    approval.BulkOrderQty
                }
            ).ToListAsync();

            var inQtyBySubmission = await _context.StoreInRecords
                .GroupBy(r => r.SubmissionId)
                .Select(g => new
                {
                    SubmissionId = g.Key,
                    TotalInQty = g.Sum(r => r.InQty),
                    EntryCount = g.Count()
                })
                .ToDictionaryAsync(x => x.SubmissionId, x => new { x.TotalInQty, x.EntryCount });

            var balances = approvals.Select(a =>
            {
                var approvedBulk = int.TryParse(a.BulkOrderQty, out var qty) ? qty : 0;
                var info = inQtyBySubmission.GetValueOrDefault(a.Id);
                var totalIn = info?.TotalInQty ?? 0;

                return new BulkBalanceDto
                {
                    SubmissionId = a.Id,
                    StyleNo = a.StyleNo,
                    CustomerName = a.CustomerName,
                    ApprovedBulkQty = approvedBulk,
                    TotalInQty = totalIn,
                    RemainingBulkQty = Math.Max(0, approvedBulk - totalIn),
                    EntryCount = info?.EntryCount ?? 0
                };
            }).ToList();

            return Ok(balances);
        }

        // ==========================================
        // STORE IN RECORDS (GET all with children)
        // ==========================================
        [HttpGet("store-in")]
        public async Task<ActionResult<IEnumerable<StoreInResponseDto>>> GetStoreInRecords()
        {
            var records = await _context.StoreInRecords
                .Include(r => r.Cuts)
                    .ThenInclude(c => c.Bundles)
                .OrderByDescending(r => r.CutInDate)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();

            return Ok(records.Select(MapToResponse));
        }

        // ==========================================
        // STORE IN — GET single by ID
        // ==========================================
        [HttpGet("store-in/{id}")]
        public async Task<ActionResult<StoreInResponseDto>> GetStoreInRecord(string id)
        {
            var record = await _context.StoreInRecords
                .Include(r => r.Cuts)
                    .ThenInclude(c => c.Bundles)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
                return NotFound();

            return Ok(MapToResponse(record));
        }

        // ==========================================
        // STORE IN — CREATE (single form, nested cuts + bundles)
        // ==========================================
        [HttpPost("store-in")]
        public async Task<ActionResult<StoreInResponseDto>> CreateStoreInRecord(CreateStoreInRequest request)
        {
            // --- Validate request ---
            if (string.IsNullOrWhiteSpace(request.SubmissionId))
                return BadRequest("SubmissionId is required.");

            if (string.IsNullOrWhiteSpace(request.ScheduleNo))
                return BadRequest("ScheduleNo is required.");

            if (request.InQty <= 0)
                return BadRequest("InQty must be greater than zero.");

            if (request.Cuts == null || request.Cuts.Count == 0)
                return BadRequest("At least one cut is required.");

            // --- Validate submission + approval ---
            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == request.SubmissionId);

            if (submission == null)
                return BadRequest("Linked submission not found.");

            if (!submission.IsLatestRevision)
                return BadRequest("Only the latest approved revision can move to Stores.");

            var approval = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == request.SubmissionId);

            if (approval == null || approval.Status != "Approved")
                return BadRequest("Only approved revisions can move to Stores.");

            var approvedBulk = int.TryParse(approval.BulkOrderQty, out var bulkQty) ? bulkQty : 0;

            // --- Check global bulk balance ---
            var existingTotalIn = await GetTotalInQtyForSubmission(request.SubmissionId);
            var remainingBulk = Math.Max(0, approvedBulk - existingTotalIn);

            if (request.InQty > remainingBulk)
                return BadRequest($"InQty ({request.InQty}) exceeds remaining bulk balance ({remainingBulk}). " +
                                  $"Approved: {approvedBulk}, Already received: {existingTotalIn}.");

            // --- Validate cuts ---
            var totalCutQty = request.Cuts.Sum(c => c.CutQty);
            if (totalCutQty > request.InQty)
                return BadRequest($"Total cut qty ({totalCutQty}) exceeds IN qty ({request.InQty}).");

            foreach (var cut in request.Cuts)
            {
                if (string.IsNullOrWhiteSpace(cut.CutNo))
                    return BadRequest("Every cut must have a CutNo.");

                if (cut.CutQty <= 0)
                    return BadRequest($"Cut '{cut.CutNo}' must have a CutQty greater than zero.");

                if (cut.Bundles == null || cut.Bundles.Count == 0)
                    return BadRequest($"Cut '{cut.CutNo}' must have at least one bundle.");

                var totalBundleQty = cut.Bundles.Sum(b => b.BundleQty);
                if (totalBundleQty > cut.CutQty)
                    return BadRequest($"Cut '{cut.CutNo}': total bundle qty ({totalBundleQty}) exceeds cut qty ({cut.CutQty}).");

                foreach (var bundle in cut.Bundles)
                {
                    if (string.IsNullOrWhiteSpace(bundle.BundleNo))
                        return BadRequest($"Cut '{cut.CutNo}': every bundle must have a BundleNo.");
                    if (bundle.BundleQty <= 0)
                        return BadRequest($"Cut '{cut.CutNo}', Bundle '{bundle.BundleNo}': BundleQty must be > 0.");
                    if (string.IsNullOrWhiteSpace(bundle.Size))
                        return BadRequest($"Cut '{cut.CutNo}', Bundle '{bundle.BundleNo}': Size is required.");
                }
            }

            // --- Get job info for display fields ---
            var job = await _context.DevelopmentJobs
                .FirstOrDefaultAsync(j =>
                    j.StyleNo.ToLower() == submission.StyleNo.ToLower() &&
                    j.Customer.ToLower() == submission.CustomerName.ToLower());

            // --- Build the entity tree ---
            var newBalanceBulk = Math.Max(0, approvedBulk - existingTotalIn - request.InQty);

            var record = new StoreInRecord
            {
                Id = Guid.NewGuid().ToString(),
                SubmissionId = request.SubmissionId,
                RevisionNo = submission.RevisionNo,
                StyleNo = submission.StyleNo,
                CustomerName = submission.CustomerName,
                BodyColour = job?.BodyColour,
                PrintColour = job?.PrintColour,
                Components = job != null ? string.Join(", ", job.Placements) : null,
                Season = job?.Season,
                ScheduleNo = request.ScheduleNo,
                CutInDate = request.CutInDate,
                BulkQty = approvedBulk,
                InQty = request.InQty,
                BalanceBulkQty = newBalanceBulk,
                TotalCutQty = totalCutQty,
                UncutBalance = Math.Max(0, request.InQty - totalCutQty),
                AvailableQty = request.InQty, // Full IN qty is available until production issues
                Cuts = request.Cuts.Select(c => new CutRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    CutNo = c.CutNo,
                    CutQty = c.CutQty,
                    Bundles = c.Bundles.Select(b => new BundleRecord
                    {
                        Id = Guid.NewGuid().ToString(),
                        BundleNo = b.BundleNo,
                        BundleQty = b.BundleQty,
                        Size = b.Size,
                        NumberRange = b.NumberRange
                    }).ToList()
                }).ToList()
            };

            _context.StoreInRecords.Add(record);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStoreInRecord), new { id = record.Id }, MapToResponse(record));
        }

        // ==========================================
        // STORE IN — UPDATE
        // ==========================================
        [HttpPut("store-in/{id}")]
        public async Task<IActionResult> UpdateStoreInRecord(string id, CreateStoreInRequest request)
        {
            var existing = await _context.StoreInRecords
                .Include(r => r.Cuts)
                    .ThenInclude(c => c.Bundles)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existing == null)
                return NotFound();

            // Check if any production records already reference this — block structural changes if so
            var hasProductionRecords = await _context.StoreProductionRecords
                .AnyAsync(p => p.StoreInRecordId == id);

            if (hasProductionRecords)
                return BadRequest("Cannot restructure a Store-In record that already has production issues. Delete the production records first.");

            // --- Validate the same way as create ---
            if (request.InQty <= 0)
                return BadRequest("InQty must be greater than zero.");

            if (request.Cuts == null || request.Cuts.Count == 0)
                return BadRequest("At least one cut is required.");

            var approval = await _context.Approvals
                .FirstOrDefaultAsync(a => a.SubmissionId == existing.SubmissionId);

            var approvedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;

            // Exclude current record from the total to allow resizing
            var existingTotalIn = await GetTotalInQtyForSubmission(existing.SubmissionId, excludeStoreInId: id);
            var remainingBulk = Math.Max(0, approvedBulk - existingTotalIn);

            if (request.InQty > remainingBulk)
                return BadRequest($"InQty ({request.InQty}) exceeds remaining bulk balance ({remainingBulk}).");

            var totalCutQty = request.Cuts.Sum(c => c.CutQty);
            if (totalCutQty > request.InQty)
                return BadRequest($"Total cut qty ({totalCutQty}) exceeds IN qty ({request.InQty}).");

            // Validate each cut and bundle
            foreach (var cut in request.Cuts)
            {
                if (string.IsNullOrWhiteSpace(cut.CutNo))
                    return BadRequest("Every cut must have a CutNo.");
                if (cut.CutQty <= 0)
                    return BadRequest($"Cut '{cut.CutNo}' must have a CutQty > 0.");
                if (cut.Bundles == null || cut.Bundles.Count == 0)
                    return BadRequest($"Cut '{cut.CutNo}' must have at least one bundle.");

                var totalBundleQty = cut.Bundles.Sum(b => b.BundleQty);
                if (totalBundleQty > cut.CutQty)
                    return BadRequest($"Cut '{cut.CutNo}': total bundle qty ({totalBundleQty}) exceeds cut qty ({cut.CutQty}).");
            }

            // --- Remove old children and replace ---
            _context.BundleRecords.RemoveRange(existing.Cuts.SelectMany(c => c.Bundles));
            _context.CutRecords.RemoveRange(existing.Cuts);

            // --- Update parent fields ---
            existing.ScheduleNo = request.ScheduleNo;
            existing.CutInDate = request.CutInDate;
            existing.InQty = request.InQty;
            existing.BalanceBulkQty = Math.Max(0, approvedBulk - existingTotalIn - request.InQty);
            existing.TotalCutQty = totalCutQty;
            existing.UncutBalance = Math.Max(0, request.InQty - totalCutQty);
            existing.AvailableQty = request.InQty; // Reset since no production records exist

            // --- Recreate children ---
            existing.Cuts = request.Cuts.Select(c => new CutRecord
            {
                Id = Guid.NewGuid().ToString(),
                StoreInRecordId = id,
                CutNo = c.CutNo,
                CutQty = c.CutQty,
                Bundles = c.Bundles.Select(b => new BundleRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    BundleNo = b.BundleNo,
                    BundleQty = b.BundleQty,
                    Size = b.Size,
                    NumberRange = b.NumberRange
                }).ToList()
            }).ToList();

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==========================================
        // STORE IN — DELETE
        // ==========================================
        [HttpDelete("store-in/{id}")]
        public async Task<IActionResult> DeleteStoreInRecord(string id)
        {
            var record = await _context.StoreInRecords
                .Include(r => r.Cuts)
                    .ThenInclude(c => c.Bundles)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null)
                return NotFound();

            // Block delete if production records exist
            var hasProduction = await _context.StoreProductionRecords
                .AnyAsync(p => p.StoreInRecordId == id);

            if (hasProduction)
                return BadRequest("Cannot delete a Store-In record with existing production issues.");

            // Block delete if CPI reports exist
            var hasCpi = await _context.CpiReports
                .AnyAsync(c => c.StoreInRecordId == id);

            if (hasCpi)
                return BadRequest("Cannot delete a Store-In record with existing QC reports.");

            _context.StoreInRecords.Remove(record); // Cascade deletes cuts + bundles
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==========================================
        // ELIGIBLE ITEMS FOR PRODUCTION
        // (only QC-passed store-in records, with per-cut breakdown)
        // ==========================================
        [HttpGet("eligible-production")]
        public async Task<ActionResult<IEnumerable<EligibleProductionDto>>> GetEligibleProductionItems()
        {
            // Get QC-passed store-in records with cuts
            var eligibleRecords = await (
                from storeIn in _context.StoreInRecords
                    .Include(s => s.Cuts)
                join cpi in _context.CpiReports
                    on storeIn.Id equals cpi.StoreInRecordId
                where cpi.InspectionStatus == "Passed"
                orderby cpi.SummaryDate descending
                select new { StoreIn = storeIn, Cpi = cpi }
            ).ToListAsync();

            // Get existing production records to calculate per-cut issued amounts
            var allProductionRecords = await _context.StoreProductionRecords.ToListAsync();

            // Get bulk balances
            var approvals = await _context.Approvals.ToListAsync();
            var inQtyBySubmission = await _context.StoreInRecords
                .GroupBy(r => r.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, TotalInQty = g.Sum(r => r.InQty) })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.TotalInQty);

            var result = eligibleRecords.Select(x =>
            {
                var storeIn = x.StoreIn;
                var cpi = x.Cpi;

                // Bulk balance
                var approval = approvals.FirstOrDefault(a => a.SubmissionId == storeIn.SubmissionId);
                var approvedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;
                var totalIn = inQtyBySubmission.GetValueOrDefault(storeIn.SubmissionId, 0);

                // Per-cut breakdown: how much of each cut has been issued
                var productionForThisStoreIn = allProductionRecords
                    .Where(p => p.StoreInRecordId == storeIn.Id)
                    .ToList();

                var cuts = storeIn.Cuts.Select(c =>
                {
                    var alreadyIssued = productionForThisStoreIn
                        .Where(p => p.CutNo == c.CutNo)
                        .Sum(p => p.IssueQty);

                    return new ProductionCutDto
                    {
                        CutRecordId = c.Id,
                        CutNo = c.CutNo,
                        CutQty = c.CutQty,
                        AlreadyIssued = alreadyIssued,
                        AvailableQty = Math.Max(0, c.CutQty - alreadyIssued)
                    };
                }).ToList();

                var totalAvailable = cuts.Sum(c => c.AvailableQty);

                return new EligibleProductionDto
                {
                    StoreInRecordId = storeIn.Id,
                    SubmissionId = storeIn.SubmissionId,
                    RevisionNo = storeIn.RevisionNo,
                    StyleNo = storeIn.StyleNo ?? string.Empty,
                    CustomerName = storeIn.CustomerName ?? string.Empty,
                    Components = storeIn.Components ?? string.Empty,
                    ScheduleNo = storeIn.ScheduleNo,
                    BodyColour = storeIn.BodyColour ?? string.Empty,
                    PrintColour = storeIn.PrintColour ?? string.Empty,
                    Season = storeIn.Season ?? string.Empty,
                    BulkQty = approvedBulk,
                    BulkBalance = Math.Max(0, approvedBulk - totalIn),
                    TotalAvailableQty = totalAvailable,
                    InspectionStatus = cpi.InspectionStatus,
                    CheckedBy = cpi.CheckedBy,
                    SummaryDate = cpi.SummaryDate,
                    Cuts = cuts
                };
            })
            .Where(x => x.TotalAvailableQty > 0)
            .ToList();

            return Ok(result);
        }

        // ==========================================
        // PRODUCTION ISSUES — BATCH CREATE
        // Accepts multiple rows at once (one per cut issue)
        // ==========================================
        [HttpPost("production/batch")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> BatchCreateProductionRecords(
            [FromBody] List<StoreProductionRecord> records)
        {
            if (records == null || records.Count == 0)
                return BadRequest("At least one production record is required.");

            var saved = new List<StoreProductionRecord>();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                    return BadRequest("StoreInRecordId is required for all records.");

                if (record.IssueQty <= 0)
                    return BadRequest($"IssueQty must be > 0 for cut '{record.CutNo}'.");

                var storeIn = await _context.StoreInRecords
                    .Include(s => s.Cuts)
                    .FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);

                if (storeIn == null)
                    return BadRequest("Linked Store-In record not found.");

                // QC gate
                var cpi = await _context.CpiReports
                    .FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);

                if (cpi == null || cpi.InspectionStatus != "Passed")
                    return BadRequest("Only QC-passed items can move to Production.");

                // Find the cut and check availability
                var cut = storeIn.Cuts.FirstOrDefault(c => c.CutNo == record.CutNo);
                if (cut == null)
                    return BadRequest($"Cut '{record.CutNo}' not found in Store-In record.");

                var alreadyIssued = await _context.StoreProductionRecords
                    .Where(p => p.StoreInRecordId == record.StoreInRecordId && p.CutNo == record.CutNo)
                    .SumAsync(p => p.IssueQty);

                var cutAvailable = Math.Max(0, cut.CutQty - alreadyIssued);

                if (record.IssueQty > cutAvailable)
                    return BadRequest($"Cut '{record.CutNo}': IssueQty ({record.IssueQty}) exceeds available ({cutAvailable}).");

                record.Id = Guid.NewGuid().ToString();
                record.SubmissionId = storeIn.SubmissionId;
                record.RevisionNo = storeIn.RevisionNo;
                record.StyleNo = storeIn.StyleNo;
                record.CustomerName = storeIn.CustomerName;
                record.Components = storeIn.Components;
                record.BalanceQty = Math.Max(0, cutAvailable - record.IssueQty);

                // Deduct from store-in available
                storeIn.AvailableQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);

                _context.StoreProductionRecords.Add(record);
                saved.Add(record);
            }

            await _context.SaveChangesAsync();
            return Ok(saved);
        }

        // ==========================================
        // PRODUCTION ISSUES — GET
        // ==========================================
        [HttpGet("production")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> GetProductionRecords()
        {
            return await _context.StoreProductionRecords
                .OrderByDescending(r => r.IssueDate)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        // ==========================================
        // PRODUCTION ISSUES — CREATE
        // ==========================================
        [HttpPost("production")]
        public async Task<ActionResult<StoreProductionRecord>> CreateProductionRecord(StoreProductionRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                return BadRequest("StoreInRecordId is required.");

            if (string.IsNullOrWhiteSpace(record.LineNo))
                return BadRequest("LineNo is required.");

            if (record.IssueQty <= 0)
                return BadRequest("IssueQty must be greater than zero.");

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            // QC gate check
            var cpi = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);

            if (cpi == null)
                return BadRequest("This item has not been inspected by QC yet.");

            if (cpi.InspectionStatus != "Passed")
                return BadRequest("Only QC-passed items can move to Production.");

            if (record.IssueQty > storeIn.AvailableQty)
                return BadRequest($"IssueQty exceeds available shelf stock ({storeIn.AvailableQty}).");

            if (string.IsNullOrWhiteSpace(record.Id))
                record.Id = Guid.NewGuid().ToString();

            // Backend source of truth
            record.SubmissionId = storeIn.SubmissionId;
            record.RevisionNo = storeIn.RevisionNo;
            record.StyleNo = storeIn.StyleNo;
            record.CustomerName = storeIn.CustomerName;
            record.Components = storeIn.Components;
            record.CutNo = storeIn.Cuts?.FirstOrDefault()?.CutNo ?? "N/A";
            record.BalanceQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);

            // Deduct from shelf
            storeIn.AvailableQty = record.BalanceQty;

            _context.StoreProductionRecords.Add(record);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductionRecords), new { id = record.Id }, record);
        }

        // ==========================================
        // PRODUCTION ISSUES — UPDATE
        // ==========================================
        [HttpPut("production/{id}")]
        public async Task<IActionResult> UpdateProductionRecord(string id, StoreProductionRecord record)
        {
            if (id != record.Id)
                return BadRequest("ID mismatch.");

            var existing = await _context.StoreProductionRecords.FindAsync(id);
            if (existing == null)
                return NotFound();

            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == existing.StoreInRecordId);

            if (storeIn == null)
                return BadRequest("Linked Store-In record not found.");

            var cpi = await _context.CpiReports
                .FirstOrDefaultAsync(r => r.StoreInRecordId == existing.StoreInRecordId);

            if (cpi == null || cpi.InspectionStatus != "Passed")
                return BadRequest("Only QC-passed items can remain in Production.");

            // Restore previous qty
            storeIn.AvailableQty += existing.IssueQty;

            if (record.IssueQty <= 0)
                return BadRequest("IssueQty must be greater than zero.");

            if (record.IssueQty > storeIn.AvailableQty)
                return BadRequest($"Updated IssueQty exceeds available shelf stock ({storeIn.AvailableQty}).");

            existing.IssueDate = record.IssueDate;
            existing.IssueQty = record.IssueQty;
            existing.LineNo = record.LineNo;
            existing.BalanceQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);

            storeIn.AvailableQty = existing.BalanceQty;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==========================================
        // PRODUCTION ISSUES — DELETE
        // ==========================================
        [HttpDelete("production/{id}")]
        public async Task<IActionResult> DeleteProductionRecord(string id)
        {
            var record = await _context.StoreProductionRecords.FindAsync(id);
            if (record == null)
                return NotFound();

            // Restore available qty
            var storeIn = await _context.StoreInRecords
                .FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);

            if (storeIn != null)
                storeIn.AvailableQty += record.IssueQty;

            _context.StoreProductionRecords.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}