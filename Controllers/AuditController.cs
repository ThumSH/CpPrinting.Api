using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Audit,Admin")] 
    [Route("api/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<AuditRecord>>> GetAuditRecords()
        {
            return await _context.AuditRecords.OrderByDescending(r => r.Date).ToListAsync();
        }

        [HttpPost("records")]
        public async Task<ActionResult<AuditRecord>> CreateAuditRecord(AuditRecord record)
        {
            _context.AuditRecords.Add(record);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAuditRecords), new { id = record.Id }, record);
        }

        // Dedicated endpoint just for updating the status (Pass/Fail)
        [HttpPatch("records/{id}/status")]
        public async Task<IActionResult> UpdateAuditStatus(string id, [FromBody] UpdateStatusDto dto)
        {
            var record = await _context.AuditRecords.FindAsync(id);
            if (record == null) return NotFound();

            record.Status = dto.Status;
            record.Remarks = dto.Remarks;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("records/{id}")]
        public async Task<IActionResult> DeleteAuditRecord(string id)
        {
            var record = await _context.AuditRecords.FindAsync(id);
            if (record == null) return NotFound();

            _context.AuditRecords.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    // A small DTO (Data Transfer Object) for the PATCH request
    public class UpdateStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}