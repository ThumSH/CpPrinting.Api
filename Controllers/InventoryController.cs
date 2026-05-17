using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.DTOs;
using CpPrinting.Api.Services;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Stores,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public InventoryController(AppDbContext context, ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task<int> GetTotalInQtyForSubmission(string submissionId, string? excludeStoreInId = null)
        {
            var query = _context.StoreInRecords.Where(r => r.SubmissionId == submissionId);
            if (!string.IsNullOrEmpty(excludeStoreInId))
                query = query.Where(r => r.Id != excludeStoreInId);
            return await query.SumAsync(r => r.InQty);
        }

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
        // ==========================================
        [HttpGet("eligible-store-in")]
        public async Task<ActionResult<IEnumerable<EligibleStoreInDto>>> GetEligibleStoreInStyles()
        {
            var approvedSubmissions = await (
                from submission in _context.Submissions
                join approval in _context.Approvals
                    on submission.Id equals approval.SubmissionId
                where submission.IsLatestRevision == true
                      && approval.Status == "Approved"
                orderby approval.ReviewedAt descending
                select new
                {
                    SubmissionId   = submission.Id,
                    submission.RevisionNo,
                    submission.StyleNo,
                    submission.CustomerName,
                    submission.SubmissionDate,
                    submission.Level,
                    ApprovalStatus = approval.Status,
                    approval.ReviewedAt,
                    approval.BulkOrderQty,
                }
            ).ToListAsync();

            if (!approvedSubmissions.Any())
                return Ok(new List<EligibleStoreInDto>());

            // Priority 1: DevelopmentJob (old flow)
            var allJobs = await _context.DevelopmentJobs
                .Select(j => new { j.StyleNo, j.Customer, j.BodyColour, j.PrintColour, j.Season, j.Placements })
                .ToListAsync();

            // Priority 2: SampleStyle bridge (new flow — bridge sets SampleStyle.Id = SubmissionId)
            var submissionIds = approvedSubmissions.Select(s => s.SubmissionId).ToList();
            var sampleStyles = await _context.SampleStyles
                .Where(s => submissionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.BodyColour, s.PrintColour, s.Season, s.Placements })
                .ToListAsync();
            var sampleStyleMap = sampleStyles.ToDictionary(s => s.Id);

            var inQtyBySubmission = await _context.StoreInRecords
                .GroupBy(r => r.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, TotalInQty = g.Sum(r => r.InQty) })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.TotalInQty);

            var eligibleStyles = approvedSubmissions.Select(x =>
            {
                var approvedBulk = int.TryParse(x.BulkOrderQty, out var qty) ? qty : 0;
                var totalUsed    = inQtyBySubmission.GetValueOrDefault(x.SubmissionId, 0);
                var remaining    = Math.Max(0, approvedBulk - totalUsed);

                var job = allJobs.FirstOrDefault(j =>
                    j.StyleNo.Equals(x.StyleNo, StringComparison.OrdinalIgnoreCase) &&
                    j.Customer.Equals(x.CustomerName, StringComparison.OrdinalIgnoreCase));

                sampleStyleMap.TryGetValue(x.SubmissionId, out var sample);

                return new EligibleStoreInDto
                {
                    SubmissionId     = x.SubmissionId,
                    RevisionNo       = x.RevisionNo,
                    StyleNo          = x.StyleNo,
                    CustomerName     = x.CustomerName,
                    SubmissionDate   = x.SubmissionDate,
                    Level            = x.Level,
                    ApprovalStatus   = x.ApprovalStatus,
                    ReviewedAt       = x.ReviewedAt,
                    BodyColour       = job?.BodyColour  ?? sample?.BodyColour  ?? string.Empty,
                    PrintColour      = job?.PrintColour ?? sample?.PrintColour ?? string.Empty,
                    Season           = job?.Season      ?? sample?.Season      ?? string.Empty,
                    Components       = job  != null ? string.Join(", ", job.Placements)
                                     : sample != null ? sample.Placements
                                     : string.Empty,
                    ApprovedBulkQty  = approvedBulk,
                    RemainingBulkQty = remaining,
                };
            })
            .Where(x => x.RemainingBulkQty > 0)
            .ToList();

            return Ok(eligibleStyles);
        }

        // ==========================================
        // BULK BALANCE
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
                .Select(g => new { SubmissionId = g.Key, TotalInQty = g.Sum(r => r.InQty), EntryCount = g.Count() })
                .ToDictionaryAsync(x => x.SubmissionId, x => new { x.TotalInQty, x.EntryCount });

            var balances = approvals.Select(a =>
            {
                var approvedBulk = int.TryParse(a.BulkOrderQty, out var qty) ? qty : 0;
                var info = inQtyBySubmission.GetValueOrDefault(a.Id);
                var totalIn = info?.TotalInQty ?? 0;
                return new BulkBalanceDto
                {
                    SubmissionId     = a.Id,
                    StyleNo          = a.StyleNo,
                    CustomerName     = a.CustomerName,
                    ApprovedBulkQty  = approvedBulk,
                    TotalInQty       = totalIn,
                    RemainingBulkQty = Math.Max(0, approvedBulk - totalIn),
                    EntryCount       = info?.EntryCount ?? 0
                };
            }).ToList();

            return Ok(balances);
        }

        // ==========================================
        // STORE IN — GET all
        // ==========================================
        [HttpGet("store-in")]
        public async Task<ActionResult<IEnumerable<StoreInResponseDto>>> GetStoreInRecords()
        {
            var records = await _context.StoreInRecords
                .Include(r => r.Cuts).ThenInclude(c => c.Bundles)
                .OrderByDescending(r => r.CutInDate)
                .ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
            return Ok(records.Select(MapToResponse));
        }

        // ==========================================
        // STORE IN — GET single
        // ==========================================
        [HttpGet("store-in/{id}")]
        public async Task<ActionResult<StoreInResponseDto>> GetStoreInRecord(string id)
        {
            var record = await _context.StoreInRecords
                .Include(r => r.Cuts).ThenInclude(c => c.Bundles)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();
            return Ok(MapToResponse(record));
        }

        // ==========================================
        // STORE IN — CREATE
        // ==========================================
        [HttpPost("store-in")]
        public async Task<ActionResult<StoreInResponseDto>> CreateStoreInRecord(CreateStoreInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SubmissionId))
                return BadRequest("SubmissionId is required.");
            if (string.IsNullOrWhiteSpace(request.ScheduleNo))
                return BadRequest("ScheduleNo is required.");
            if (string.IsNullOrWhiteSpace(request.CutInDate))
                return BadRequest("Cut In Date is required.");
            if (request.InQty <= 0)
                return BadRequest("InQty must be greater than zero.");
            if (request.Cuts == null || request.Cuts.Count == 0)
                return BadRequest("At least one cut is required.");

            var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == request.SubmissionId);
            if (submission == null) return BadRequest("Linked submission not found.");
            if (!submission.IsLatestRevision) return BadRequest("Only the latest approved revision can move to Stores.");

            var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == request.SubmissionId);
            if (approval == null || approval.Status != "Approved")
                return BadRequest("Only approved revisions can move to Stores.");

            var approvedBulk = int.TryParse(approval.BulkOrderQty, out var bulkQty) ? bulkQty : 0;

            var scheduleDuplicate = await _context.StoreInRecords
                .AnyAsync(r => r.SubmissionId == request.SubmissionId &&
                               r.ScheduleNo.ToLower() == request.ScheduleNo.Trim().ToLower());
            if (scheduleDuplicate)
                return BadRequest($"Schedule No '{request.ScheduleNo}' already exists for this style. Each store-in entry must have a unique schedule number.");

            var existingTotalIn = await GetTotalInQtyForSubmission(request.SubmissionId);
            var remainingBulk = Math.Max(0, approvedBulk - existingTotalIn);
            if (request.InQty > remainingBulk)
                return BadRequest($"InQty ({request.InQty}) exceeds remaining bulk balance ({remainingBulk}). Approved: {approvedBulk}, Already received: {existingTotalIn}.");

            var totalCutQty = request.Cuts.Sum(c => c.CutQty);
            if (totalCutQty > request.InQty)
                return BadRequest($"Total cut qty ({totalCutQty}) exceeds IN qty ({request.InQty}).");

            var cutNos = request.Cuts.Select(c => c.CutNo.Trim().ToLower()).ToList();
            if (cutNos.Count != cutNos.Distinct().Count())
                return BadRequest("Duplicate cut numbers found. Each cut must have a unique number.");

            foreach (var cut in request.Cuts)
            {
                if (string.IsNullOrWhiteSpace(cut.CutNo)) return BadRequest("Every cut must have a CutNo.");
                if (cut.CutQty <= 0) return BadRequest($"Cut '{cut.CutNo}' must have a CutQty greater than zero.");
                if (cut.Bundles == null || cut.Bundles.Count == 0) return BadRequest($"Cut '{cut.CutNo}' must have at least one bundle.");
                var totalBundleQty = cut.Bundles.Sum(b => b.BundleQty);
                if (totalBundleQty > cut.CutQty)
                    return BadRequest($"Cut '{cut.CutNo}': total bundle qty ({totalBundleQty}) exceeds cut qty ({cut.CutQty}).");
                var bundleNos = cut.Bundles.Select(b => b.BundleNo.Trim().ToLower()).ToList();
                if (bundleNos.Count != bundleNos.Distinct().Count())
                    return BadRequest($"Cut '{cut.CutNo}': duplicate bundle numbers found.");
                foreach (var bundle in cut.Bundles)
                {
                    if (string.IsNullOrWhiteSpace(bundle.BundleNo)) return BadRequest($"Cut '{cut.CutNo}': every bundle must have a BundleNo.");
                    if (bundle.BundleQty <= 0) return BadRequest($"Cut '{cut.CutNo}', Bundle '{bundle.BundleNo}': BundleQty must be > 0.");
                    if (string.IsNullOrWhiteSpace(bundle.Size)) return BadRequest($"Cut '{cut.CutNo}', Bundle '{bundle.BundleNo}': Size is required.");
                }
            }

            // ── Resolve style details: DevelopmentJob first, SampleStyle as fallback ──
            var job = await _context.DevelopmentJobs
                .FirstOrDefaultAsync(j =>
                    j.StyleNo.ToLower() == submission.StyleNo.ToLower() &&
                    j.Customer.ToLower() == submission.CustomerName.ToLower());

            var sampleStyleFallback = job == null
                ? await _context.SampleStyles.FirstOrDefaultAsync(s => s.Id == request.SubmissionId)
                : null;

            // ✅ These resolved variables are what gets stamped onto StoreInRecord
            // and flow to CPI, Production, Gatepass, Audit, Worker unchanged
            var bodyColour  = job?.BodyColour  ?? sampleStyleFallback?.BodyColour  ?? string.Empty;
            var printColour = job?.PrintColour ?? sampleStyleFallback?.PrintColour ?? string.Empty;
            var season      = job?.Season      ?? sampleStyleFallback?.Season      ?? string.Empty;
            var components  = job != null
                ? string.Join(", ", job.Placements)
                : sampleStyleFallback?.Placements ?? string.Empty;

            var newBalanceBulk = Math.Max(0, approvedBulk - existingTotalIn - request.InQty);

            var record = new StoreInRecord
            {
                Id           = Guid.NewGuid().ToString(),
                SubmissionId = request.SubmissionId,
                RevisionNo   = submission.RevisionNo,
                StyleNo      = submission.StyleNo,
                CustomerName = submission.CustomerName,
                // ✅ Using resolved variables — not job?.X directly
                BodyColour   = bodyColour,
                PrintColour  = printColour,
                Components   = components,
                Season       = season,
                ScheduleNo   = request.ScheduleNo,
                CutInDate    = request.CutInDate,
                BulkQty      = approvedBulk,
                InQty        = request.InQty,
                BalanceBulkQty = newBalanceBulk,
                TotalCutQty    = totalCutQty,
                UncutBalance   = Math.Max(0, request.InQty - totalCutQty),
                AvailableQty   = request.InQty,
                Cuts = request.Cuts.Select(c => new CutRecord
                {
                    Id     = Guid.NewGuid().ToString(),
                    CutNo  = c.CutNo,
                    CutQty = c.CutQty,
                    Bundles = c.Bundles.Select(b => new BundleRecord
                    {
                        Id          = Guid.NewGuid().ToString(),
                        BundleNo    = b.BundleNo,
                        BundleQty   = b.BundleQty,
                        Size        = b.Size,
                        NumberRange = b.NumberRange
                    }).ToList()
                }).ToList()
            };

            _context.StoreInRecords.Add(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "StoreIn", record.Id,
                $"Created store-in for {record.StyleNo} ({record.BodyColour}) — {record.InQty} pcs, Schedule: {record.ScheduleNo}");

            return CreatedAtAction(nameof(GetStoreInRecord), new { id = record.Id }, MapToResponse(record));
        }

        // ==========================================
        // STORE IN — UPDATE
        // ==========================================
        [HttpPut("store-in/{id}")]
        public async Task<IActionResult> UpdateStoreInRecord(string id, CreateStoreInRequest request)
        {
            var existing = await _context.StoreInRecords
                .Include(r => r.Cuts).ThenInclude(c => c.Bundles)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (existing == null) return NotFound();

            if (await _context.StoreProductionRecords.AnyAsync(p => p.StoreInRecordId == id))
                return BadRequest("Cannot edit: production records already issued from this store-in.");
            if (await _context.CpiReports.AnyAsync(c => c.StoreInRecordId == id))
                return BadRequest("Cannot edit: QC inspection has already been performed on this record.");
            if (await _context.AdviceNotes.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot edit: Gatepass advice notes reference this record.");
            if (await _context.AuditRecords.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot edit: Audit records reference this record.");

            if (string.IsNullOrWhiteSpace(request.CutInDate)) return BadRequest("Cut In Date is required.");
            if (request.InQty <= 0) return BadRequest("InQty must be greater than zero.");
            if (request.Cuts == null || request.Cuts.Count == 0) return BadRequest("At least one cut is required.");

            var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == existing.SubmissionId);
            var approvedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;

            if (!string.IsNullOrWhiteSpace(request.ScheduleNo))
            {
                var scheduleDuplicate = await _context.StoreInRecords
                    .AnyAsync(r => r.SubmissionId == existing.SubmissionId && r.Id != id &&
                                   r.ScheduleNo.ToLower() == request.ScheduleNo.Trim().ToLower());
                if (scheduleDuplicate)
                    return BadRequest($"Schedule No '{request.ScheduleNo}' already exists for this style.");
            }

            var existingTotalIn = await GetTotalInQtyForSubmission(existing.SubmissionId, excludeStoreInId: id);
            var remainingBulk = Math.Max(0, approvedBulk - existingTotalIn);
            if (request.InQty > remainingBulk)
                return BadRequest($"InQty ({request.InQty}) exceeds remaining bulk balance ({remainingBulk}).");

            var totalCutQty = request.Cuts.Sum(c => c.CutQty);
            if (totalCutQty > request.InQty)
                return BadRequest($"Total cut qty ({totalCutQty}) exceeds IN qty ({request.InQty}).");

            var cutNos = request.Cuts.Select(c => c.CutNo.Trim().ToLower()).ToList();
            if (cutNos.Count != cutNos.Distinct().Count())
                return BadRequest("Duplicate cut numbers found.");

            foreach (var cut in request.Cuts)
            {
                if (string.IsNullOrWhiteSpace(cut.CutNo)) return BadRequest("Every cut must have a CutNo.");
                if (cut.CutQty <= 0) return BadRequest($"Cut '{cut.CutNo}' must have a CutQty > 0.");
                if (cut.Bundles == null || cut.Bundles.Count == 0) return BadRequest($"Cut '{cut.CutNo}' must have at least one bundle.");
                var totalBundleQty = cut.Bundles.Sum(b => b.BundleQty);
                if (totalBundleQty > cut.CutQty)
                    return BadRequest($"Cut '{cut.CutNo}': bundle total ({totalBundleQty}) exceeds cut qty ({cut.CutQty}).");
                var bundleNos = cut.Bundles.Select(b => b.BundleNo.Trim().ToLower()).ToList();
                if (bundleNos.Count != bundleNos.Distinct().Count())
                    return BadRequest($"Cut '{cut.CutNo}': duplicate bundle numbers.");
                foreach (var bundle in cut.Bundles)
                {
                    if (string.IsNullOrWhiteSpace(bundle.BundleNo)) return BadRequest($"Cut '{cut.CutNo}': every bundle must have a BundleNo.");
                    if (bundle.BundleQty <= 0) return BadRequest($"Cut '{cut.CutNo}', Bundle '{bundle.BundleNo}': BundleQty must be > 0.");
                    if (string.IsNullOrWhiteSpace(bundle.Size)) return BadRequest($"Cut '{cut.CutNo}', Bundle '{bundle.BundleNo}': Size is required.");
                }
            }

            _context.BundleRecords.RemoveRange(existing.Cuts.SelectMany(c => c.Bundles));
            _context.CutRecords.RemoveRange(existing.Cuts);

            existing.ScheduleNo    = request.ScheduleNo;
            existing.CutInDate     = request.CutInDate;
            existing.InQty         = request.InQty;
            existing.BalanceBulkQty = Math.Max(0, approvedBulk - existingTotalIn - request.InQty);
            existing.TotalCutQty   = totalCutQty;
            existing.UncutBalance  = Math.Max(0, request.InQty - totalCutQty);
            existing.AvailableQty  = request.InQty;
            existing.Cuts = request.Cuts.Select(c => new CutRecord
            {
                Id = Guid.NewGuid().ToString(),
                StoreInRecordId = id,
                CutNo  = c.CutNo,
                CutQty = c.CutQty,
                Bundles = c.Bundles.Select(b => new BundleRecord
                {
                    Id          = Guid.NewGuid().ToString(),
                    BundleNo    = b.BundleNo,
                    BundleQty   = b.BundleQty,
                    Size        = b.Size,
                    NumberRange = b.NumberRange
                }).ToList()
            }).ToList();

            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Update", "StoreIn", id,
                $"Updated store-in for {existing.StyleNo} — {existing.InQty} pcs");
            return NoContent();
        }

        // ==========================================
        // STORE IN — DELETE
        // ==========================================
        [HttpDelete("store-in/{id}")]
        public async Task<IActionResult> DeleteStoreInRecord(string id)
        {
            var record = await _context.StoreInRecords
                .Include(r => r.Cuts).ThenInclude(c => c.Bundles)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();

            if (await _context.StoreProductionRecords.AnyAsync(p => p.StoreInRecordId == id))
                return BadRequest("Cannot delete a Store-In record with existing production issues.");
            if (await _context.CpiReports.AnyAsync(c => c.StoreInRecordId == id))
                return BadRequest("Cannot delete: QC inspection reports exist for this record.");
            if (await _context.AdviceNotes.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot delete: Gatepass advice notes exist for this record.");
            if (await _context.AuditRecords.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot delete: Audit records exist for this record.");

            _context.StoreInRecords.Remove(record);
            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Delete", "StoreIn", id,
                $"Deleted store-in for {record.StyleNo} ({record.InQty} pcs)");
            return NoContent();
        }

        // ==========================================
        // ELIGIBLE ITEMS FOR PRODUCTION
        // ==========================================
        [HttpGet("eligible-production")]
        public async Task<ActionResult<IEnumerable<EligibleProductionDto>>> GetEligibleProductionItems()
        {
            var eligibleRecords = await (
                from storeIn in _context.StoreInRecords.Include(s => s.Cuts)
                join cpi in _context.CpiReports on storeIn.Id equals cpi.StoreInRecordId
                where cpi.InspectionStatus == "Passed"
                orderby cpi.SummaryDate descending
                select new { StoreIn = storeIn, Cpi = cpi }
            ).ToListAsync();

            var allProductionRecords = await _context.StoreProductionRecords.ToListAsync();
            var approvals = await _context.Approvals.ToListAsync();
            var inQtyBySubmission = await _context.StoreInRecords
                .GroupBy(r => r.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, TotalInQty = g.Sum(r => r.InQty) })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.TotalInQty);

            var result = eligibleRecords.Select(x =>
            {
                var storeIn = x.StoreIn;
                var cpi     = x.Cpi;
                var approval = approvals.FirstOrDefault(a => a.SubmissionId == storeIn.SubmissionId);
                var approvedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;
                var totalIn = inQtyBySubmission.GetValueOrDefault(storeIn.SubmissionId, 0);
                var productionForThisStoreIn = allProductionRecords.Where(p => p.StoreInRecordId == storeIn.Id).ToList();

                var cuts = storeIn.Cuts.Select(c =>
                {
                    var alreadyIssued = productionForThisStoreIn.Where(p => p.CutNo == c.CutNo).Sum(p => p.IssueQty);
                    var cpiCut = cpi.CutInspections?.FirstOrDefault(ci => ci.CutNo == c.CutNo);
                    return new ProductionCutDto
                    {
                        CutRecordId   = c.Id,
                        CutNo         = c.CutNo,
                        CutQty        = c.CutQty,
                        Part          = cpiCut?.Part ?? string.Empty,
                        AlreadyIssued = alreadyIssued,
                        AvailableQty  = Math.Max(0, c.CutQty - alreadyIssued)
                    };
                }).ToList();

                return new EligibleProductionDto
                {
                    StoreInRecordId   = storeIn.Id,
                    SubmissionId      = storeIn.SubmissionId,
                    RevisionNo        = storeIn.RevisionNo,
                    StyleNo           = storeIn.StyleNo ?? string.Empty,
                    CustomerName      = storeIn.CustomerName ?? string.Empty,
                    Components        = storeIn.Components ?? string.Empty,
                    ScheduleNo        = storeIn.ScheduleNo,
                    BodyColour        = storeIn.BodyColour ?? string.Empty,
                    PrintColour       = storeIn.PrintColour ?? string.Empty,
                    Season            = storeIn.Season ?? string.Empty,
                    BulkQty           = approvedBulk,
                    BulkBalance       = Math.Max(0, approvedBulk - totalIn),
                    TotalAvailableQty = cuts.Sum(c => c.AvailableQty),
                    InspectionStatus  = cpi.InspectionStatus,
                    CheckedBy         = cpi.CheckedBy,
                    SummaryDate       = cpi.SummaryDate,
                    Cuts              = cuts
                };
            })
            .Where(x => x.TotalAvailableQty > 0)
            .ToList();

            return Ok(result);
        }

        // ==========================================
        // PRODUCTION ISSUES — BATCH CREATE
        // ==========================================
        [HttpPost("production/batch")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> BatchCreateProductionRecords(
            [FromBody] List<StoreProductionRecord> records)
        {
            if (records == null || !records.Any())
                return BadRequest("No production records provided.");

            var createdRecords = new List<StoreProductionRecord>();
            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                    return BadRequest("StoreInRecordId is required for all production records.");
                var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);
                if (storeIn == null) return BadRequest($"Store-In record not found for ID: {record.StoreInRecordId}");
                var cpiReport = await _context.CpiReports.FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);
                if (cpiReport == null) return BadRequest($"CPI Report not found for Store-In ID: {record.StoreInRecordId}. Items must pass CPI first.");
                if (cpiReport.InspectionStatus != "Passed" && cpiReport.InspectionStatus != "Pending")
                    return BadRequest($"Cannot issue to production. CPI status is '{cpiReport.InspectionStatus}'.");
                var previouslyIssued = await _context.StoreProductionRecords
                    .Where(p => p.StoreInRecordId == record.StoreInRecordId && p.CutNo == record.CutNo)
                    .SumAsync(p => p.IssueQty);
                var cutRecord = await _context.CutRecords.FirstOrDefaultAsync(c => c.StoreInRecordId == record.StoreInRecordId && c.CutNo == record.CutNo);
                var maxAllowed = cutRecord?.CutQty ?? 0;
                if (previouslyIssued + record.IssueQty > maxAllowed)
                    return BadRequest($"Cannot issue {record.IssueQty} for Cut {record.CutNo}. Only {maxAllowed - previouslyIssued} remaining.");
                record.Id           = Guid.NewGuid().ToString();
                record.SubmissionId = storeIn.SubmissionId;
                record.RevisionNo   = storeIn.RevisionNo;
                record.IssueDate    = DateTime.Now.ToString("yyyy-MM-dd");
                record.StyleNo      = storeIn.StyleNo;
                record.CustomerName = storeIn.CustomerName;
                record.BalanceQty   = record.IssueQty;
                _context.StoreProductionRecords.Add(record);
                createdRecords.Add(record);
            }
            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Create", "Production",
                string.Join(",", createdRecords.Select(r => r.Id)),
                $"Batch issued {createdRecords.Count} records to production.");
            return Ok(createdRecords);
        }

        [HttpGet("production")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> GetProductionRecords()
        {
            return await _context.StoreProductionRecords
                .OrderByDescending(r => r.IssueDate).ThenByDescending(r => r.RevisionNo)
                .ToListAsync();
        }

        [HttpPost("production")]
        public async Task<ActionResult<StoreProductionRecord>> CreateProductionRecord(StoreProductionRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.StoreInRecordId)) return BadRequest("StoreInRecordId is required.");
            if (string.IsNullOrWhiteSpace(record.LineNo)) return BadRequest("LineNo is required.");
            if (record.IssueQty <= 0) return BadRequest("IssueQty must be greater than zero.");
            var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);
            if (storeIn == null) return BadRequest("Linked Store-In record not found.");
            var cpi = await _context.CpiReports.FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);
            if (cpi == null) return BadRequest("This item has not been inspected by QC yet.");
            if (cpi.InspectionStatus != "Passed") return BadRequest("Only QC-passed items can move to Production.");
            if (record.IssueQty > storeIn.AvailableQty) return BadRequest($"IssueQty exceeds available shelf stock ({storeIn.AvailableQty}).");
            if (string.IsNullOrWhiteSpace(record.Id)) record.Id = Guid.NewGuid().ToString();
            record.SubmissionId = storeIn.SubmissionId;
            record.RevisionNo   = storeIn.RevisionNo;
            record.StyleNo      = storeIn.StyleNo;
            record.CustomerName = storeIn.CustomerName;
            var storeInCpi = await _context.CpiReports.Include(r => r.CutInspections).FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);
            var cutNoToUse = !string.IsNullOrWhiteSpace(record.CutNo) ? record.CutNo : (storeIn.Cuts?.FirstOrDefault()?.CutNo ?? "N/A");
            var cpiCutForSingle = storeInCpi?.CutInspections?.FirstOrDefault(ci => ci.CutNo == cutNoToUse);
            if (string.IsNullOrWhiteSpace(record.Components))
                record.Components = cpiCutForSingle?.Part ?? storeIn.Components;
            record.CutNo      = cutNoToUse;
            record.BalanceQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);
            storeIn.AvailableQty = record.BalanceQty;
            _context.StoreProductionRecords.Add(record);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProductionRecords), new { id = record.Id }, record);
        }

        [HttpPut("production/{id}")]
        public async Task<IActionResult> UpdateProductionRecord(string id, StoreProductionRecord record)
        {
            if (id != record.Id) return BadRequest("ID mismatch.");
            var existing = await _context.StoreProductionRecords.FindAsync(id);
            if (existing == null) return NotFound();
            var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(r => r.Id == existing.StoreInRecordId);
            if (storeIn == null) return BadRequest("Linked Store-In record not found.");
            var cpi = await _context.CpiReports.FirstOrDefaultAsync(r => r.StoreInRecordId == existing.StoreInRecordId);
            if (cpi == null || cpi.InspectionStatus != "Passed") return BadRequest("Only QC-passed items can remain in Production.");
            storeIn.AvailableQty += existing.IssueQty;
            if (record.IssueQty <= 0) return BadRequest("IssueQty must be greater than zero.");
            if (record.IssueQty > storeIn.AvailableQty) return BadRequest($"Updated IssueQty exceeds available shelf stock ({storeIn.AvailableQty}).");
            existing.IssueDate   = record.IssueDate;
            existing.IssueQty    = record.IssueQty;
            existing.LineNo      = record.LineNo;
            existing.BalanceQty  = Math.Max(0, storeIn.AvailableQty - record.IssueQty);
            storeIn.AvailableQty = existing.BalanceQty;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("production/{id}")]
        public async Task<IActionResult> DeleteProductionRecord(string id)
        {
            var record = await _context.StoreProductionRecords.FindAsync(id);
            if (record == null) return NotFound();
            var hasAdviceNotes = await _context.AdviceNotes
                .AnyAsync(a => a.ProductionRecordId.Contains(id) || a.StoreInRecordId == record.StoreInRecordId);
            if (hasAdviceNotes) return BadRequest("Cannot delete: Gatepass advice notes have been dispatched from this production record.");
            var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);
            if (storeIn != null) storeIn.AvailableQty += record.IssueQty;
            _context.StoreProductionRecords.Remove(record);
            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Delete", "Production", id,
                $"Deleted production record for {record.StyleNo}, Cut: {record.CutNo} ({record.IssueQty} pcs)");
            return NoContent();
        }

        [HttpGet("store-in/locks")]
        public async Task<ActionResult> GetStoreInLocks()
        {
            var storeInIds = await _context.StoreInRecords.Select(s => s.Id).ToListAsync();
            var cpiIds   = await _context.CpiReports.Select(c => c.StoreInRecordId).Distinct().ToListAsync();
            var prodIds  = await _context.StoreProductionRecords.Select(p => p.StoreInRecordId).Distinct().ToListAsync();
            var gateIds  = await _context.AdviceNotes.Select(a => a.StoreInRecordId).Distinct().ToListAsync();
            var auditIds = await _context.AuditRecords.Select(a => a.StoreInRecordId).Distinct().ToListAsync();
            var locks = storeInIds.ToDictionary(id => id, id => new
            {
                HasCpi        = cpiIds.Contains(id),
                HasProduction = prodIds.Contains(id),
                HasGatepass   = gateIds.Contains(id),
                HasAudit      = auditIds.Contains(id),
                IsLocked      = cpiIds.Contains(id) || prodIds.Contains(id) || gateIds.Contains(id) || auditIds.Contains(id)
            });
            return Ok(locks);
        }

        [HttpGet("production/locks")]
        public async Task<ActionResult> GetProductionLocks()
        {
            var prodIds = await _context.StoreProductionRecords.Select(p => p.Id).ToListAsync();
            var allAdviceNotes = await _context.AdviceNotes.Select(a => a.ProductionRecordId).ToListAsync();
            var gatepassProdIds = allAdviceNotes
                .SelectMany(p => (p ?? "").Split(',').Select(x => x.Trim()))
                .Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
            var locks = prodIds.ToDictionary(id => id, id => new { IsLocked = gatepassProdIds.Contains(id) });
            return Ok(locks);
        }
    }
}