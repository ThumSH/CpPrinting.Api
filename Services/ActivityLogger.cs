using System.Security.Claims;
using CpPrinting.Api.Data;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Services
{
    public class ActivityLogger
    {
        private readonly AppDbContext _context;

        public ActivityLogger(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Log an activity from an HTTP context (extracts user from JWT claims).
        /// </summary>
        public async Task Log(ClaimsPrincipal user, HttpContext httpContext, string action, string entity, string entityId, string description)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst("sub")?.Value
                         ?? "unknown";
            var userName = user.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                var operatorName = httpContext.Request.Headers["X-Operator-Name"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(operatorName))
                 userName = $"{operatorName} ({userName})";

            var log = new ActivityLog
            {
                UserId = userId,
                UserName = userName,
                UserRole = userRole,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                Description = description,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                IpAddress = ip,
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Log a login event (before JWT is issued, so we pass details directly).
        /// </summary>
        public async Task LogLogin(string userId, string userName, string userRole, string ip)
        {
            var log = new ActivityLog
            {
                UserId = userId,
                UserName = userName,
                UserRole = userRole,
                Action = "Login",
                Entity = "Auth",
                EntityId = userId,
                Description = $"{userName} ({userRole}) logged in",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                IpAddress = ip,
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}