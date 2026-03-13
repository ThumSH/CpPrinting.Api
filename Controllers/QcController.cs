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
    public class QcController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QcController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("reports")]
        public async Task<ActionResult<IEnumerable<CPIReport>>> GetCPIReports()
        {
            return await _context.CpiReports.OrderByDescending(r => r.Date).ToListAsync();
        }

        [HttpPost("reports")]
        public async Task<ActionResult<CPIReport>> CreateCPIReport(CPIReport report)
        {
            _context.CpiReports.Add(report);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCPIReports), new { id = report.Id }, report);
        }

        [HttpPut("reports/{id}")]
        public async Task<IActionResult> UpdateCPIReport(string id, CPIReport report)
        {
            if (id != report.Id) return BadRequest("ID mismatch");

            _context.Entry(report).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("reports/{id}")]
        public async Task<IActionResult> DeleteCPIReport(string id)
        {
            var report = await _context.CpiReports.FindAsync(id);
            if (report == null) return NotFound();

            _context.CpiReports.Remove(report);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}