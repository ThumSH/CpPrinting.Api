using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Gatepass,Admin")] 
    [Route("api/[controller]")]
    [ApiController]
    public class GatepassController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GatepassController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("advicenotes")]
        public async Task<ActionResult<IEnumerable<AdviceNoteRecord>>> GetAdviceNotes()
        {
            return await _context.AdviceNotes.OrderByDescending(n => n.DeliveryDate).ToListAsync();
        }

        [HttpPost("advicenotes")]
        public async Task<ActionResult<AdviceNoteRecord>> CreateAdviceNote(AdviceNoteRecord note)
        {
            _context.AdviceNotes.Add(note);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAdviceNotes), new { id = note.Id }, note);
        }

        [HttpPut("advicenotes/{id}")]
        public async Task<IActionResult> UpdateAdviceNote(string id, AdviceNoteRecord note)
        {
            if (id != note.Id) return BadRequest();
            _context.Entry(note).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("advicenotes/{id}")]
        public async Task<IActionResult> DeleteAdviceNote(string id)
        {
            var note = await _context.AdviceNotes.FindAsync(id);
            if (note == null) return NotFound();
            _context.AdviceNotes.Remove(note);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}