using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Audit,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;
        private static readonly string[] AllowedStatuses = { "Pass", "Fail" };

        public AuditController(AppDbContext context, ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("eligible")]
        public async Task<ActionResult> GetEligibleAuditItems()
        {
            var storeInRecords = await _context.StoreInRecords
                .Include(s => s.Cuts)
                    .ThenInclude(c => c.Bundles)
                .OrderByDescending(s => s.CutInDate)
                .ToListAsync();

            var result = storeInRecords.Select(s => new
            {
                s.Id,
                s.SubmissionId,
                s.RevisionNo,
                StyleNo = s.StyleNo ?? string.Empty,
                CustomerName = s.CustomerName ?? string.Empty,
                ScheduleNo = s.ScheduleNo,
                BodyColour = s.BodyColour ?? string.Empty,
                Cuts = s.Cuts.Select(c => new
                {
                    c.Id,
                    c.CutNo,
                    c.CutQty,
                    Bundles = c.Bundles.Select(b => new
                    {
                        b.Id,
                        b.BundleNo,
                        b.BundleQty,
                        b.Size,
                        NumberRange = b.NumberRange ?? string.Empty
                    })
                })
            });

            return Ok(result);
        }

        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<AuditRecord>>> GetAuditRecords()
        {
            return await _context.AuditRecords
                .OrderByDescending(r => r.Date)
                .ToListAsync();
        }

        [HttpPost("records/batch")]
        public async Task<ActionResult<IEnumerable<AuditRecord>>> BatchCreateAuditRecords(
            [FromBody] List<AuditRecord> records)
        {
            if (records == null || records.Count == 0)
                return BadRequest("At least one record is required.");

            var saved = new List<AuditRecord>();

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StoreInRecordId))
                    return BadRequest("StoreInRecordId is required.");

                if (!AllowedStatuses.Contains(record.Status))
                    return BadRequest($"Invalid Status '{record.Status}'. Allowed: Pass, Fail.");

                // Block duplicate at the BUNDLE level only.
                // A cut can be audited multiple times. Each individual bundle can only be
                // audited once per (storeIn, cut). This matches the frontend, which allows
                // partial-cut audits when leftovers can form valid AQL ranges.
                var existingForCut = await _context.AuditRecords
                    .Where(a => a.StoreInRecordId == record.StoreInRecordId && a.CutNo == record.CutNo)
                    .ToListAsync();

                if (existingForCut.Count > 0)
                {
                    var alreadyAuditedBundleNos = new HashSet<string>();
                    foreach (var ar in existingForCut)
                    {
                        if (ar.Bundles == null) continue;
                        foreach (var b in ar.Bundles)
                        {
                            alreadyAuditedBundleNos.Add(b.BundleNo);
                        }
                    }

                    var requestedBundles = record.Bundles ?? new List<AuditBundleSelection>();
                    var conflicts = new List<string>();
                    foreach (var rb in requestedBundles)
                    {
                        if (alreadyAuditedBundleNos.Contains(rb.BundleNo))
                            conflicts.Add(rb.BundleNo);
                    }

                    if (conflicts.Count > 0)
                        return BadRequest($"Bundle(s) [{string.Join(", ", conflicts)}] of Cut '{record.CutNo}' have already been audited.");
                }

                var storeIn = await _context.StoreInRecords
                    .FirstOrDefaultAsync(s => s.Id == record.StoreInRecordId);

                if (storeIn == null)
                    return BadRequest("Linked Store-In record not found.");

                record.Id = Guid.NewGuid().ToString();
                record.SubmissionId = storeIn.SubmissionId;
                record.RevisionNo = storeIn.RevisionNo;
                record.StyleNo = storeIn.StyleNo ?? string.Empty;
                record.CustomerName = storeIn.CustomerName ?? string.Empty;
                record.ScheduleNo = storeIn.ScheduleNo;
                record.Colour = storeIn.BodyColour ?? string.Empty;

                _context.AuditRecords.Add(record);
                saved.Add(record);
            }

            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "Audit", string.Join(",", saved.Select(r => r.Id)),
                $"Created {saved.Count} audit(s) for {saved.FirstOrDefault()?.StyleNo}, Cut: {saved.FirstOrDefault()?.CutNo} — {saved.Select(r => r.Status).Distinct().First()}");

            return Ok(saved);
        }

        [HttpDelete("records/{id}")]
        public async Task<IActionResult> DeleteAuditRecord(string id)
        {
            var record = await _context.AuditRecords.FindAsync(id);
            if (record == null) return NotFound();

            _context.AuditRecords.Remove(record);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "Audit", id,
                $"Deleted audit for {record.StyleNo}, Cut: {record.CutNo}");

            return NoContent();
        }
    }
}