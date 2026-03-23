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

                // Block duplicate: same store-in + cut already audited
                var alreadyAudited = await _context.AuditRecords
                    .AnyAsync(a => a.StoreInRecordId == record.StoreInRecordId && a.CutNo == record.CutNo);

                if (alreadyAudited)
                    return BadRequest($"Cut '{record.CutNo}' has already been audited for this store-in record.");

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