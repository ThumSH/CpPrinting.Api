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

        // ── Helper: total IN qty already used for a submission (component) ────
        private async Task<int> GetTotalInQtyForSubmission(string submissionId, string? excludeStoreInId = null)
        {
            // Count only cuts that belong to this specific submission/component
            var query = _context.CutRecords
                .Where(c => c.SubmissionId == submissionId);

            if (!string.IsNullOrEmpty(excludeStoreInId))
                query = query.Where(c => c.StoreInRecordId != excludeStoreInId);

            return await query.SumAsync(c => c.CutQty);
        }

        private static StoreInResponseDto MapToResponse(StoreInRecord record)
        {
            return new StoreInResponseDto
            {
                Id             = record.Id,
                SubmissionId   = record.SubmissionId,
                RevisionNo     = record.RevisionNo,
                StyleNo        = record.StyleNo ?? string.Empty,
                CustomerName   = record.CustomerName ?? string.Empty,
                BodyColour     = record.BodyColour ?? string.Empty,
                PrintColour    = record.PrintColour ?? string.Empty,
                Components     = record.Components ?? string.Empty,
                Season         = record.Season ?? string.Empty,
                ScheduleNo     = record.ScheduleNo,
                CutInDate      = record.CutInDate ?? string.Empty,
                BulkQty        = record.BulkQty,
                InQty          = record.InQty,
                BalanceBulkQty = record.BalanceBulkQty,
                TotalCutQty    = record.TotalCutQty,
                UncutBalance   = record.UncutBalance,
                AvailableQty   = record.AvailableQty,
                Cuts = record.Cuts.Select(c => new CutResponseDto
                {
                    Id     = c.Id,
                    CutNo  = c.CutNo,
                    CutQty = c.CutQty,
                    SubmissionId = c.SubmissionId,
                    Bundles = c.Bundles.Select(b => new BundleResponseDto
                    {
                        Id          = b.Id,
                        BundleNo    = b.BundleNo,
                        BundleQty   = b.BundleQty,
                        Size        = b.Size,
                        NumberRange = b.NumberRange ?? string.Empty
                    }).ToList()
                }).ToList()
            };
        }

        // ==========================================
        // ELIGIBLE STYLES FOR STORE IN
        //
        // Returns one EligibleStoreInDto per approved component-submission.
        // The frontend groups these by StyleNo + CustomerName so the user
        // sees one style card with multiple component rows underneath.
        // ==========================================
        [HttpGet("eligible-store-in")]
        public async Task<ActionResult<IEnumerable<EligibleStoreInDto>>> GetEligibleStoreInStyles()
        {
            // Sum ALL approved revisions per style+component group.
            // Each revision contributes its extra BulkOrderQty.
            // Grouping key: StyleNo + CustomerName + Component (via SampleStyle bridge).
            var allApproved = await (
                from submission in _context.Submissions
                join approval in _context.Approvals
                    on submission.Id equals approval.SubmissionId
                where approval.Status == "Approved"
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

            if (!allApproved.Any())
                return Ok(new List<EligibleStoreInDto>());

            // SampleStyles for component + colour info (new flow bridge)
            var allSubIds = allApproved.Select(s => s.SubmissionId).ToList();
            var sampleStyles = await _context.SampleStyles
                .Where(s => allSubIds.Contains(s.Id))
                .Select(s => new { s.Id, s.BodyColour, s.PrintColour, s.Season, s.Component })
                .ToListAsync();
            var sampleMap = sampleStyles.ToDictionary(s => s.Id);

            // DevelopmentJobs fallback (old flow)
            var allJobs = await _context.DevelopmentJobs
                .Select(j => new { j.StyleNo, j.Customer, j.BodyColour, j.PrintColour, j.Season, j.Component })
                .ToListAsync();

            // Cut qty already received per submissionId
            var cutQtyBySub = await _context.CutRecords
                .GroupBy(c => c.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, Total = g.Sum(c => c.CutQty) })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.Total);

            // Group all revisions by style+customer+component
            var groups = allApproved.GroupBy(x =>
            {
                sampleMap.TryGetValue(x.SubmissionId, out var s);
                return $"{x.StyleNo}||{x.CustomerName}||{s?.Component ?? string.Empty}";
            });

            var result = new List<EligibleStoreInDto>();

            foreach (var group in groups)
            {
                var revisions = group.OrderByDescending(r => r.RevisionNo).ToList();
                var latest    = revisions.First();

                sampleMap.TryGetValue(latest.SubmissionId, out var latestSample);
                var job = latestSample == null
                    ? allJobs.FirstOrDefault(j =>
                        j.StyleNo.Equals(latest.StyleNo, StringComparison.OrdinalIgnoreCase) &&
                        j.Customer.Equals(latest.CustomerName, StringComparison.OrdinalIgnoreCase))
                    : null;

                // Total approved bulk = SUM of all revisions
                var totalBulk = revisions.Sum(r => int.TryParse(r.BulkOrderQty, out var q) ? q : 0);

                // Total used = cuts across ALL revision submissionIds for this group
                var totalUsed = revisions.Sum(r => cutQtyBySub.GetValueOrDefault(r.SubmissionId, 0));

                var remaining = Math.Max(0, totalBulk - totalUsed);
                if (remaining <= 0) continue;

                result.Add(new EligibleStoreInDto
                {
                    SubmissionId     = latest.SubmissionId,
                    RevisionNo       = latest.RevisionNo,
                    StyleNo          = latest.StyleNo,
                    CustomerName     = latest.CustomerName,
                    SubmissionDate   = latest.SubmissionDate,
                    Level            = latest.Level,
                    ApprovalStatus   = latest.ApprovalStatus,
                    ReviewedAt       = latest.ReviewedAt,
                    BodyColour       = latestSample?.BodyColour  ?? job?.BodyColour  ?? string.Empty,
                    PrintColour      = latestSample?.PrintColour ?? job?.PrintColour ?? string.Empty,
                    Season           = latestSample?.Season      ?? job?.Season      ?? string.Empty,
                    Components       = latestSample?.Component   ?? job?.Component   ?? string.Empty,
                    ApprovedBulkQty  = totalBulk,
                    RemainingBulkQty = remaining,
                });
            }

            return Ok(result);
        }

        // ==========================================
        // BULK BALANCE — sums ALL approved revisions per component group
        // ==========================================
        [HttpGet("bulk-balance")]
        public async Task<ActionResult<IEnumerable<BulkBalanceDto>>> GetBulkBalances()
        {
            var allApproved = await (
                from submission in _context.Submissions
                join approval in _context.Approvals
                    on submission.Id equals approval.SubmissionId
                where approval.Status == "Approved"
                select new { submission.Id, submission.StyleNo, submission.CustomerName, approval.BulkOrderQty }
            ).ToListAsync();

            var allSubIds = allApproved.Select(a => a.Id).ToList();
            var components = await _context.SampleStyles
                .Where(s => allSubIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Component, s.BodyColour })
                .ToDictionaryAsync(s => s.Id);

            var cutQtyBySub = await _context.CutRecords
                .GroupBy(c => c.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, Total = g.Sum(c => c.CutQty),
                    Entries = g.Select(c => c.StoreInRecordId).Distinct().Count() })
                .ToDictionaryAsync(x => x.SubmissionId, x => new { x.Total, x.Entries });

            // Group by style+customer+component, same as GetEligibleStoreInStyles
            var groups = allApproved.GroupBy(a =>
            {
                components.TryGetValue(a.Id, out var s);
                return $"{a.StyleNo}||{a.CustomerName}||{s?.Component ?? string.Empty}";
            });

            var result = new List<BulkBalanceDto>();
            foreach (var group in groups)
            {
                var revisions = group.ToList();
                var firstSub  = revisions.First();
                components.TryGetValue(firstSub.Id, out var comp);

                var totalBulk  = revisions.Sum(r => int.TryParse(r.BulkOrderQty, out var q) ? q : 0);
                var totalUsed  = revisions.Sum(r => cutQtyBySub.GetValueOrDefault(r.Id)?.Total ?? 0);
                var entryCount = revisions.Sum(r => cutQtyBySub.GetValueOrDefault(r.Id)?.Entries ?? 0);

                result.Add(new BulkBalanceDto
                {
                    SubmissionId     = firstSub.Id,
                    StyleNo          = firstSub.StyleNo,
                    CustomerName     = firstSub.CustomerName,
                    Component        = comp?.Component ?? string.Empty,
                    BodyColour       = comp?.BodyColour ?? string.Empty,
                    ApprovedBulkQty  = totalBulk,
                    TotalInQty       = totalUsed,
                    RemainingBulkQty = Math.Max(0, totalBulk - totalUsed),
                    EntryCount       = entryCount,
                });
            }

            return Ok(result);
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
        //
        // Each cut in the request must carry a SubmissionId (which component
        // it belongs to). The backend validates per-component bulk balance.
        // The StoreInRecord itself stores a summary of all components in
        // the Components field (e.g. "Front, Back") for display.
        // ==========================================
        [HttpPost("store-in")]
        public async Task<ActionResult<StoreInResponseDto>> CreateStoreInRecord(CreateStoreInRequest request)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.ScheduleNo))
                return BadRequest("ScheduleNo is required.");
            if (string.IsNullOrWhiteSpace(request.CutInDate))
                return BadRequest("Cut In Date is required.");
            if (request.InQty <= 0)
                return BadRequest("InQty must be greater than zero.");
            if (request.Cuts == null || request.Cuts.Count == 0)
                return BadRequest("At least one cut is required.");

            // All cuts must carry a SubmissionId
            var missingSubId = request.Cuts.FirstOrDefault(c => string.IsNullOrWhiteSpace(c.SubmissionId));
            if (missingSubId != null)
                return BadRequest($"Cut '{missingSubId.CutNo}' is missing a SubmissionId (component assignment).");

            // Collect all unique submission IDs from cuts
            var submissionIds = request.Cuts
                .Select(c => c.SubmissionId)
                .Distinct()
                .ToList();

            // Validate all submissions are approved
            foreach (var subId in submissionIds)
            {
                var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == subId);
                if (submission == null) return BadRequest($"Submission {subId} not found.");
                // All approved revisions are valid — no IsLatestRevision check needed

                var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == subId);
                if (approval == null || approval.Status != "Approved")
                    return BadRequest($"Submission {subId} has not been approved.");
            }

            // Per-component bulk balance validation
            // CRITICAL: Must sum ALL approved revisions for the same style+component group,
            // not just the single submissionId's approval. A style may have multiple revisions
            // (Rev1=1000 + Rev2=100 = 1100 total) each with their own SubmissionId.
            var cutsBySubmission = request.Cuts
                .GroupBy(c => c.SubmissionId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.CutQty));

            foreach (var kvp in cutsBySubmission)
            {
                var subId     = kvp.Key;
                var cutQtySum = kvp.Value;

                // Find the SampleStyle for this submission to get style+component identity
                var thisSample = await _context.SampleStyles.FirstOrDefaultAsync(s => s.Id == subId);
                var label      = thisSample?.Component ?? subId;

                // Sum ALL approved revisions for the same StyleNo + CustomerName + Component
                // This is the same grouping logic used in GetEligibleStoreInStyles
                int totalApprovedBulk = 0;
                int totalExistingUsed = 0;

                if (thisSample != null)
                {
                    // Find all SampleStyles with same style+customer+component
                    var siblingStyleIds = await _context.SampleStyles
                        .Where(s => s.StyleNo == thisSample.StyleNo
                                 && s.Customer == thisSample.Customer
                                 && s.Component == thisSample.Component)
                        .Select(s => s.Id)
                        .ToListAsync();

                    // Sum bulk across all approved revisions in this group
                    var siblingApprovals = await (
                        from submission in _context.Submissions
                        join approval in _context.Approvals
                            on submission.Id equals approval.SubmissionId
                        where siblingStyleIds.Contains(submission.Id)
                              && approval.Status == "Approved"
                        select approval.BulkOrderQty
                    ).ToListAsync();

                    totalApprovedBulk = siblingApprovals.Sum(q =>
                        int.TryParse(q, out var parsed) ? parsed : 0);

                    // Sum all cuts already received across ALL sibling submissions
                    foreach (var sibId in siblingStyleIds)
                        totalExistingUsed += await GetTotalInQtyForSubmission(sibId);
                }
                else
                {
                    // Fallback: check just this submission (old flow without SampleStyle)
                    var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == subId);
                    totalApprovedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;
                    totalExistingUsed = await GetTotalInQtyForSubmission(subId);
                }

                var remaining = Math.Max(0, totalApprovedBulk - totalExistingUsed);

                if (cutQtySum > remaining)
                {
                    return BadRequest(
                        $"Component '{label}': cut qty ({cutQtySum}) exceeds remaining bulk balance ({remaining}). " +
                        $"Total approved across all revisions: {totalApprovedBulk}, Already received: {totalExistingUsed}.");
                }
            }

            // Validate cuts and bundles
            var totalCutQty = request.Cuts.Sum(c => c.CutQty);
            if (totalCutQty > request.InQty)
                return BadRequest($"Total cut qty ({totalCutQty}) exceeds IN qty ({request.InQty}).");

            var cutNos = request.Cuts.Select(c => c.CutNo.Trim().ToLower()).ToList();
            if (cutNos.Count != cutNos.Distinct().Count())
                return BadRequest("Duplicate cut numbers found.");

            foreach (var cut in request.Cuts)
            {
                if (string.IsNullOrWhiteSpace(cut.CutNo)) return BadRequest("Every cut must have a CutNo.");
                if (cut.CutQty <= 0) return BadRequest($"Cut '{cut.CutNo}' must have CutQty > 0.");
                if (cut.Bundles == null || cut.Bundles.Count == 0) return BadRequest($"Cut '{cut.CutNo}' must have at least one bundle.");
                var totalBundleQty = cut.Bundles.Sum(b => b.BundleQty);
                if (totalBundleQty > cut.CutQty)
                    return BadRequest($"Cut '{cut.CutNo}': bundle total ({totalBundleQty}) exceeds cut qty ({cut.CutQty}).");
                var bundleNos = cut.Bundles.Select(b => b.BundleNo.Trim().ToLower()).ToList();
                if (bundleNos.Count != bundleNos.Distinct().Count())
                    return BadRequest($"Cut '{cut.CutNo}': duplicate bundle numbers.");
                foreach (var bundle in cut.Bundles)
                {
                    if (string.IsNullOrWhiteSpace(bundle.BundleNo)) return BadRequest($"Cut '{cut.CutNo}': every bundle needs a BundleNo.");
                    if (bundle.BundleQty <= 0) return BadRequest($"Bundle '{bundle.BundleNo}': BundleQty must be > 0.");
                    if (string.IsNullOrWhiteSpace(bundle.Size)) return BadRequest($"Bundle '{bundle.BundleNo}': Size is required.");
                }
            }

            // Resolve display fields from the primary submission (first SubmissionId)
            var primarySubId = submissionIds.First();
            var primarySub   = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == primarySubId);
            var styleNo      = primarySub?.StyleNo ?? string.Empty;
            var customerName = primarySub?.CustomerName ?? string.Empty;

            // Load all SampleStyles for display fields (component, colour, season)
            var allSamples = await _context.SampleStyles
                .Where(s => submissionIds.Contains(s.Id))
                .ToListAsync();

            // Components summary for display: "Front, Back"
            var componentsSummary = string.Join(", ", allSamples
                .OrderBy(s => s.Component)
                .Select(s => s.Component)
                .Distinct());

            // Use first sample for shared fields (bodyColour shown per-cut via component)
            var primarySample = allSamples.FirstOrDefault(s => s.Id == primarySubId);
            var season = primarySample?.Season ?? string.Empty;

            // For BalanceBulkQty display: sum remaining across all components
            int displayApprovedBulk = 0;
            int displayExistingIn   = 0;
            foreach (var subId in submissionIds)
            {
                var appr = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == subId);
                displayApprovedBulk += (appr != null && int.TryParse(appr.BulkOrderQty, out var bq)) ? bq : 0;
                displayExistingIn   += await GetTotalInQtyForSubmission(subId);
            }

            var record = new StoreInRecord
            {
                Id           = Guid.NewGuid().ToString(),
                SubmissionId = primarySubId,  // primary component for backward compat
                RevisionNo   = primarySub?.RevisionNo ?? 1,
                StyleNo      = styleNo,
                CustomerName = customerName,
                BodyColour   = primarySample?.BodyColour ?? string.Empty,
                PrintColour  = primarySample?.PrintColour ?? string.Empty,
                Components   = componentsSummary,
                Season       = season,
                ScheduleNo   = request.ScheduleNo,
                CutInDate    = request.CutInDate,
                BulkQty      = displayApprovedBulk,
                InQty        = request.InQty,
                BalanceBulkQty = Math.Max(0, displayApprovedBulk - displayExistingIn - request.InQty),
                TotalCutQty  = totalCutQty,
                UncutBalance = Math.Max(0, request.InQty - totalCutQty),
                AvailableQty = request.InQty,
                Cuts = request.Cuts.Select(c => new CutRecord
                {
                    Id              = Guid.NewGuid().ToString(),
                    CutNo           = c.CutNo,
                    CutQty          = c.CutQty,
                    SubmissionId    = c.SubmissionId,  // ← tracks which component
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
                $"Created store-in for {record.StyleNo} ({componentsSummary}) — {record.InQty} pcs, Sch: {record.ScheduleNo}");

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
                return BadRequest("Cannot edit: production records already issued.");
            if (await _context.CpiReports.AnyAsync(c => c.StoreInRecordId == id))
                return BadRequest("Cannot edit: QC inspection already performed.");
            if (await _context.AdviceNotes.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot edit: Gatepass notes reference this record.");
            if (await _context.AuditRecords.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot edit: Audit records reference this record.");

            if (string.IsNullOrWhiteSpace(request.CutInDate)) return BadRequest("Cut In Date is required.");
            if (request.InQty <= 0) return BadRequest("InQty must be greater than zero.");
            if (request.Cuts == null || request.Cuts.Count == 0) return BadRequest("At least one cut is required.");

            var totalCutQty = request.Cuts.Sum(c => c.CutQty);
            if (totalCutQty > request.InQty)
                return BadRequest($"Total cut qty ({totalCutQty}) exceeds IN qty ({request.InQty}).");

            // Re-validate per-component bulk (excluding this record's current cuts)
            var submissionIds = request.Cuts.Select(c => c.SubmissionId).Distinct().ToList();
            var cutsBySubmission = request.Cuts
                .GroupBy(c => c.SubmissionId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.CutQty));

            foreach (var kvp in cutsBySubmission)
            {
                var subId     = kvp.Key;
                var cutQtySum = kvp.Value;

                // Sum ALL revisions for same style+component group (same fix as CreateStoreInRecord)
                var thisSample = await _context.SampleStyles.FirstOrDefaultAsync(s => s.Id == subId);
                int totalApprovedBulk = 0;
                int totalExistingUsed = 0;

                if (thisSample != null)
                {
                    var siblingIds = await _context.SampleStyles
                        .Where(s => s.StyleNo == thisSample.StyleNo
                                 && s.Customer == thisSample.Customer
                                 && s.Component == thisSample.Component)
                        .Select(s => s.Id).ToListAsync();

                    var siblingApprovals = await (
                        from submission in _context.Submissions
                        join approval in _context.Approvals on submission.Id equals approval.SubmissionId
                        where siblingIds.Contains(submission.Id) && approval.Status == "Approved"
                        select approval.BulkOrderQty
                    ).ToListAsync();

                    totalApprovedBulk = siblingApprovals.Sum(q => int.TryParse(q, out var p) ? p : 0);
                    foreach (var sibId in siblingIds)
                        totalExistingUsed += await GetTotalInQtyForSubmission(sibId, excludeStoreInId: id);
                }
                else
                {
                    var approval = await _context.Approvals.FirstOrDefaultAsync(a => a.SubmissionId == subId);
                    totalApprovedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;
                    totalExistingUsed = await GetTotalInQtyForSubmission(subId, excludeStoreInId: id);
                }

                var remaining = Math.Max(0, totalApprovedBulk - totalExistingUsed);
                if (cutQtySum > remaining)
                    return BadRequest(
                        $"Component cut qty ({cutQtySum}) exceeds remaining bulk ({remaining}). " +
                        $"Total approved across all revisions: {totalApprovedBulk}, Already received: {totalExistingUsed}.");
            }

            _context.BundleRecords.RemoveRange(existing.Cuts.SelectMany(c => c.Bundles));
            _context.CutRecords.RemoveRange(existing.Cuts);

            existing.ScheduleNo    = request.ScheduleNo;
            existing.CutInDate     = request.CutInDate;
            existing.InQty         = request.InQty;
            existing.TotalCutQty   = totalCutQty;
            existing.UncutBalance  = Math.Max(0, request.InQty - totalCutQty);
            existing.AvailableQty  = request.InQty;
            existing.Cuts = request.Cuts.Select(c => new CutRecord
            {
                Id           = Guid.NewGuid().ToString(),
                StoreInRecordId = id,
                CutNo        = c.CutNo,
                CutQty       = c.CutQty,
                SubmissionId = c.SubmissionId,
                Bundles = c.Bundles.Select(b => new BundleRecord
                {
                    Id = Guid.NewGuid().ToString(), BundleNo = b.BundleNo,
                    BundleQty = b.BundleQty, Size = b.Size, NumberRange = b.NumberRange
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
                return BadRequest("Cannot delete: production records exist.");
            if (await _context.CpiReports.AnyAsync(c => c.StoreInRecordId == id))
                return BadRequest("Cannot delete: QC reports exist.");
            if (await _context.AdviceNotes.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot delete: Gatepass notes exist.");
            if (await _context.AuditRecords.AnyAsync(a => a.StoreInRecordId == id))
                return BadRequest("Cannot delete: Audit records exist.");

            _context.StoreInRecords.Remove(record);
            await _context.SaveChangesAsync();
            await _logger.Log(User, HttpContext, "Delete", "StoreIn", id,
                $"Deleted store-in for {record.StyleNo} ({record.InQty} pcs)");
            return NoContent();
        }

        // ==========================================
        // ELIGIBLE ITEMS FOR PRODUCTION — unchanged
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
            var cutQtyBySubmission = await _context.CutRecords
                .GroupBy(c => c.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, TotalCutQty = g.Sum(c => c.CutQty) })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.TotalCutQty);

            var result = eligibleRecords.Select(x =>
            {
                var storeIn = x.StoreIn;
                var cpi     = x.Cpi;
                var approval = approvals.FirstOrDefault(a => a.SubmissionId == storeIn.SubmissionId);
                var approvedBulk = (approval != null && int.TryParse(approval.BulkOrderQty, out var bq)) ? bq : 0;
                var totalIn = cutQtyBySubmission.GetValueOrDefault(storeIn.SubmissionId, 0);
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
        // PRODUCTION RECORDS — all unchanged
        // ==========================================

        [HttpPost("production/batch")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> BatchCreateProductionRecords(
            [FromBody] List<StoreProductionRecord> records)
        {
            if (records == null || !records.Any()) return BadRequest("No production records provided.");
            var createdRecords = new List<StoreProductionRecord>();
            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StoreInRecordId)) return BadRequest("StoreInRecordId is required.");
                var storeIn = await _context.StoreInRecords.FirstOrDefaultAsync(r => r.Id == record.StoreInRecordId);
                if (storeIn == null) return BadRequest($"Store-In record not found: {record.StoreInRecordId}");
                var cpiReport = await _context.CpiReports.FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);
                if (cpiReport == null) return BadRequest($"CPI Report not found for Store-In ID: {record.StoreInRecordId}.");
                if (cpiReport.InspectionStatus != "Passed" && cpiReport.InspectionStatus != "Pending")
                    return BadRequest($"Cannot issue to production. CPI status is '{cpiReport.InspectionStatus}'.");
                var previouslyIssued = await _context.StoreProductionRecords
                    .Where(p => p.StoreInRecordId == record.StoreInRecordId && p.CutNo == record.CutNo)
                    .SumAsync(p => p.IssueQty);
                var cutRecord = await _context.CutRecords.FirstOrDefaultAsync(c => c.StoreInRecordId == record.StoreInRecordId && c.CutNo == record.CutNo);
                var maxAllowed = cutRecord?.CutQty ?? 0;
                if (previouslyIssued + record.IssueQty > maxAllowed)
                    return BadRequest($"Cannot issue {record.IssueQty} for Cut {record.CutNo}. Only {maxAllowed - previouslyIssued} remaining.");
                record.Id = Guid.NewGuid().ToString();
                record.SubmissionId = storeIn.SubmissionId;
                record.RevisionNo   = storeIn.RevisionNo;
                record.IssueDate    = DateTime.Now.ToString("yyyy-MM-dd");
                record.StyleNo      = storeIn.StyleNo;
                record.CustomerName = storeIn.CustomerName;
                record.BalanceQty   = record.IssueQty;

                // Stamp Components — prefer CPI Part for this cut, fall back to StoreIn.Components
                if (string.IsNullOrWhiteSpace(record.Components))
                {
                    var cpiWithCuts = await _context.CpiReports
                        .Include(r => r.CutInspections)
                        .FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);
                    var cpiCut = cpiWithCuts?.CutInspections?.FirstOrDefault(ci => ci.CutNo == record.CutNo);
                    record.Components = cpiCut?.Part ?? storeIn.Components ?? string.Empty;
                }

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
            record.RevisionNo = storeIn.RevisionNo;
            record.StyleNo = storeIn.StyleNo;
            record.CustomerName = storeIn.CustomerName;
            var storeInCpi = await _context.CpiReports.Include(r => r.CutInspections).FirstOrDefaultAsync(r => r.StoreInRecordId == record.StoreInRecordId);
            var cutNoToUse = !string.IsNullOrWhiteSpace(record.CutNo) ? record.CutNo : (storeIn.Cuts?.FirstOrDefault()?.CutNo ?? "N/A");
            var cpiCutForSingle = storeInCpi?.CutInspections?.FirstOrDefault(ci => ci.CutNo == cutNoToUse);
            if (string.IsNullOrWhiteSpace(record.Components))
                record.Components = cpiCutForSingle?.Part ?? storeIn.Components;
            record.CutNo = cutNoToUse;
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
            existing.IssueDate = record.IssueDate;
            existing.IssueQty = record.IssueQty;
            existing.LineNo = record.LineNo;
            existing.BalanceQty = Math.Max(0, storeIn.AvailableQty - record.IssueQty);
            storeIn.AvailableQty = existing.BalanceQty;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Returns all production records for a specific line number.
        /// Used by the Worker page to show what's allocated to their line.
        /// </summary>
        [HttpGet("production/by-line/{lineNo}")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> GetProductionByLine(string lineNo)
        {
            var records = await _context.StoreProductionRecords
                .Where(r => r.LineNo == lineNo)
                .OrderByDescending(r => r.IssueDate)
                .ToListAsync();
            return Ok(records);
        }

        /// <summary>
        /// Returns distinct line numbers that have production records.
        /// Used by the Worker page for line selection.
        /// </summary>
        [HttpGet("production/lines")]
        public async Task<ActionResult<IEnumerable<string>>> GetProductionLines()
        {
            var lines = await _context.StoreProductionRecords
                .Where(r => !string.IsNullOrEmpty(r.LineNo))
                .Select(r => r.LineNo!)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();
            return Ok(lines);
        }

        [HttpDelete("production/{id}")]
        public async Task<IActionResult> DeleteProductionRecord(string id)
        {
            var record = await _context.StoreProductionRecords.FindAsync(id);
            if (record == null) return NotFound();
            var hasAdviceNotes = await _context.AdviceNotes
                .AnyAsync(a => a.ProductionRecordId.Contains(id) || a.StoreInRecordId == record.StoreInRecordId);
            if (hasAdviceNotes) return BadRequest("Cannot delete: Gatepass advice notes reference this production record.");
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