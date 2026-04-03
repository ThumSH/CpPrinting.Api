using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OperatorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OperatorController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get operators for a specific role. Called after login to show "Who is using?" screen.
        /// No auth required — called right after login before the app loads.
        /// </summary>
        [Authorize]
        [HttpGet("by-role/{role}")]
        public async Task<ActionResult> GetOperatorsByRole(string role)
        {
            var operators = await _context.Operators
                .Where(o => o.Role == role && o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name, o.Role })
                .ToListAsync();

            return Ok(operators);
        }

        /// <summary>
        /// Admin: Get all operators
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult> GetAllOperators()
        {
            var operators = await _context.Operators
                .OrderBy(o => o.Role)
                .ThenBy(o => o.Name)
                .ToListAsync();

            return Ok(operators);
        }

        /// <summary>
        /// Admin: Create operator
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult> CreateOperator(Operator op)
        {
            if (string.IsNullOrWhiteSpace(op.Name))
                return BadRequest("Name is required.");
            if (string.IsNullOrWhiteSpace(op.Role))
                return BadRequest("Role is required.");

            op.Id = Guid.NewGuid().ToString();
            op.IsActive = true;

            _context.Operators.Add(op);
            await _context.SaveChangesAsync();

            return Ok(op);
        }

        /// <summary>
        /// Admin: Update operator
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateOperator(string id, Operator op)
        {
            var existing = await _context.Operators.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = op.Name;
            existing.Role = op.Role;
            existing.IsActive = op.IsActive;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>
        /// Admin: Delete operator
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteOperator(string id)
        {
            var op = await _context.Operators.FindAsync(id);
            if (op == null) return NotFound();

            _context.Operators.Remove(op);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}