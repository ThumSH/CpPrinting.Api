using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Stores,Admin")] // Only Stores and Admins
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
        // STORE IN RECORDS
        // ==========================================

        [HttpGet("store-in")]
        public async Task<ActionResult<IEnumerable<StoreInRecord>>> GetStoreInRecords()
        {
            return await _context.StoreInRecords.OrderByDescending(r => r.CutInDate).ToListAsync();
        }

        [HttpPost("store-in")]
        public async Task<ActionResult<StoreInRecord>> CreateStoreInRecord(StoreInRecord record)
        {
            _context.StoreInRecords.Add(record);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetStoreInRecords), new { id = record.Id }, record);
        }

        [HttpPut("store-in/{id}")]
        public async Task<IActionResult> UpdateStoreInRecord(string id, StoreInRecord record)
        {
            if (id != record.Id) return BadRequest();
            _context.Entry(record).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("store-in/{id}")]
        public async Task<IActionResult> DeleteStoreInRecord(string id)
        {
            var record = await _context.StoreInRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.StoreInRecords.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==========================================
        // PRODUCTION ISSUES
        // ==========================================

        [HttpGet("production")]
        public async Task<ActionResult<IEnumerable<StoreProductionRecord>>> GetProductionRecords()
        {
            return await _context.StoreProductionRecords.OrderByDescending(r => r.IssueDate).ToListAsync();
        }

        [HttpPost("production")]
        public async Task<ActionResult<StoreProductionRecord>> CreateProductionRecord(StoreProductionRecord record)
        {
            _context.StoreProductionRecords.Add(record);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProductionRecords), new { id = record.Id }, record);
        }
        
        [HttpPut("production/{id}")]
        public async Task<IActionResult> UpdateProductionRecord(string id, StoreProductionRecord record)
        {
            if (id != record.Id) return BadRequest();
            _context.Entry(record).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("production/{id}")]
        public async Task<IActionResult> DeleteProductionRecord(string id)
        {
            var record = await _context.StoreProductionRecords.FindAsync(id);
            if (record == null) return NotFound();
            _context.StoreProductionRecords.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}