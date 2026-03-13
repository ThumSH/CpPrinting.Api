using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "QC,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class DeliveryTrackerController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeliveryTrackerController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("reports")]
        public async Task<ActionResult<IEnumerable<DeliveryTrackerReport>>> GetReports()
        {
            return await _context.DeliveryTrackers.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        [HttpPost("reports")]
        public async Task<ActionResult<DeliveryTrackerReport>> CreateReport(DeliveryTrackerReport report)
        {
            _context.DeliveryTrackers.Add(report);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetReports), new { id = report.Id }, report);
        }

        [HttpPut("reports/{id}")]
        public async Task<IActionResult> UpdateReport(string id, DeliveryTrackerReport report)
        {
            if (id != report.Id) return BadRequest();
            _context.Entry(report).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("reports/{id}")]
        public async Task<IActionResult> DeleteReport(string id)
        {
            var report = await _context.DeliveryTrackers.FindAsync(id);
            if (report == null) return NotFound();
            _context.DeliveryTrackers.Remove(report);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}