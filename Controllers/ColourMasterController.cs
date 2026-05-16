using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;
using System.Text.RegularExpressions;

namespace CpPrinting.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ColourMasterController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Valid format: "XX-1 — Descriptive Name"
        // Code: 1-5 uppercase letters, hyphen, 1-2 digits
        // Separator: exactly " — " (space em-dash space)
        // Name: starts with a letter, at least 2 chars total after separator
        private static readonly Regex NameFormat = new(
            @"^[A-Z]{1,5}-\d{1,2} — [A-Za-z][A-Za-z0-9\s\-]{1,49}$",
            RegexOptions.Compiled
        );

        private const string FORMAT_HINT =
            "Use the format: CODE — Name  (e.g. \"W-1 — White\", \"GR-2 — Grey Melange\"). " +
            "Code must be 1-5 uppercase letters, a hyphen, then 1-2 digits.";

        public ColourMasterController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET api/colourmaster
        /// Returns all active colours, sorted by SortOrder then Name.
        /// Available to ALL authenticated roles — used by the Developer dropdown.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ColourMaster>>> GetColours(
            [FromQuery] bool includeInactive = false)
        {
            var query = _context.ColourMasters.AsQueryable();

            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            var colours = await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Ok(colours);
        }

        /// <summary>
        /// GET api/colourmaster/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ColourMaster>> GetColour(string id)
        {
            var colour = await _context.ColourMasters.FindAsync(id);
            if (colour == null) return NotFound();
            return Ok(colour);
        }

        /// <summary>
        /// POST api/colourmaster
        /// Admin/Developer — create a new colour entry.
        /// </summary>
        [Authorize(Roles = "Admin,Developer")]
        [HttpPost]
        public async Task<ActionResult<ColourMaster>> CreateColour(ColourMaster colour)
        {
            if (string.IsNullOrWhiteSpace(colour.Name))
                return BadRequest("Colour name is required.");

            colour.Name = colour.Name.Trim();

            if (!NameFormat.IsMatch(colour.Name))
                return BadRequest(FORMAT_HINT);

            // Prevent exact duplicate names (case-insensitive)
            var exists = await _context.ColourMasters
                .AnyAsync(c => c.Name.ToLower() == colour.Name.ToLower());

            if (exists)
                return BadRequest($"A colour named '{colour.Name}' already exists.");

            colour.Id = Guid.NewGuid().ToString();
            colour.IsActive = true;
            colour.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

            _context.ColourMasters.Add(colour);
            await _context.SaveChangesAsync();

            return Ok(colour);
        }

        /// <summary>
        /// PUT api/colourmaster/{id}
        /// Admin only — update name, hex, sort order, active status.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<ActionResult<ColourMaster>> UpdateColour(string id, ColourMaster colour)
        {
            var existing = await _context.ColourMasters.FindAsync(id);
            if (existing == null) return NotFound();

            if (string.IsNullOrWhiteSpace(colour.Name))
                return BadRequest("Colour name is required.");

            colour.Name = colour.Name.Trim();

            if (!NameFormat.IsMatch(colour.Name))
                return BadRequest(FORMAT_HINT);

            // Check duplicate name (excluding self)
            var duplicateName = await _context.ColourMasters
                .AnyAsync(c => c.Id != id && c.Name.ToLower() == colour.Name.ToLower());

            if (duplicateName)
                return BadRequest($"A colour named '{colour.Name}' already exists.");

            existing.Name = colour.Name;
            existing.HexCode = colour.HexCode?.Trim();
            existing.IsActive = colour.IsActive;
            existing.SortOrder = colour.SortOrder;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>
        /// DELETE api/colourmaster/{id}
        /// Admin only — hard delete. Safe because DevelopmentJob stores
        /// the colour as a string value, not a foreign key.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteColour(string id)
        {
            var colour = await _context.ColourMasters.FindAsync(id);
            if (colour == null) return NotFound();

            _context.ColourMasters.Remove(colour);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// PATCH api/colourmaster/{id}/toggle
        /// Admin only — quick soft-delete toggle (active/inactive).
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/toggle")]
        public async Task<ActionResult<ColourMaster>> ToggleActive(string id)
        {
            var colour = await _context.ColourMasters.FindAsync(id);
            if (colour == null) return NotFound();

            colour.IsActive = !colour.IsActive;
            await _context.SaveChangesAsync();

            return Ok(colour);
        }
    }
}