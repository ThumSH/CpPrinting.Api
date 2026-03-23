using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Data;

namespace CpPrinting.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class ActivityLogController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ActivityLogController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get activity logs with optional filters. Returns latest 200 by default.
        /// Query params: ?action=Login&entity=StoreIn&user=sarath&limit=50&from=2026-01-01&to=2026-12-31
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetLogs(
            [FromQuery] string? action,
            [FromQuery] string? entity,
            [FromQuery] string? user,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int limit = 200)
        {
            var query = _context.ActivityLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.Action == action);

            if (!string.IsNullOrWhiteSpace(entity))
                query = query.Where(l => l.Entity == entity);

            if (!string.IsNullOrWhiteSpace(user))
                query = query.Where(l => l.UserName.Contains(user) || l.UserId == user);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(l => string.Compare(l.Timestamp, from) >= 0);

            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(l => string.Compare(l.Timestamp, to + " 23:59:59") <= 0);

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(Math.Min(limit, 500))
                .ToListAsync();

            return Ok(logs);
        }

        /// <summary>
        /// Get summary stats for the admin dashboard widget.
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var todayLogs = await _context.ActivityLogs
                .Where(l => l.Timestamp.StartsWith(today))
                .ToListAsync();

            var totalToday = todayLogs.Count;
            var loginsToday = todayLogs.Count(l => l.Action == "Login");
            var createsToday = todayLogs.Count(l => l.Action == "Create");
            var updatesToday = todayLogs.Count(l => l.Action == "Update");
            var deletesToday = todayLogs.Count(l => l.Action == "Delete");

            // Active users today
            var activeUsers = todayLogs
                .Select(l => new { l.UserName, l.UserRole })
                .Distinct()
                .ToList();

            // Recent 10 logs
            var recentLogs = await _context.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .Select(l => new { l.UserName, l.UserRole, l.Action, l.Entity, l.Description, l.Timestamp, l.IpAddress })
                .ToListAsync();

            return Ok(new
            {
                totalToday,
                loginsToday,
                createsToday,
                updatesToday,
                deletesToday,
                activeUsers,
                recentLogs,
            });
        }
    }
}