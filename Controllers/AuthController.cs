using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CpPrinting.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;

namespace CpPrinting.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const int IdleWarningSeconds = 300;      // 5 minutes idle before warning
        private const int IdleLogoutGraceSeconds = 60;   // 1 minute warning countdown
        private const int SessionTimeoutSeconds = 1800;  // used to close stale sessions during next login/heartbeat

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ActivityLogger _logger;

        public AuthController(AppDbContext context, IConfiguration configuration, ActivityLogger logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // --- DTOS ---
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }

        public class HeartbeatRequest
        {
            public string SessionId { get; set; } = string.Empty;
            public int ActiveSeconds { get; set; }
            public int IdleSeconds { get; set; }
            public bool IsIdle { get; set; }
        }

        public class LogoutRequest
        {
            public string SessionId { get; set; } = string.Empty;
            public int ActiveSeconds { get; set; }
            public int IdleSeconds { get; set; }
            public string LogoutType { get; set; } = "User";
            public string LogoutReason { get; set; } = "Manual Logout";
        }

        public class SessionEventRequest
        {
            public string SessionId { get; set; } = string.Empty;
            public string EventType { get; set; } = string.Empty;
            public int ActiveSeconds { get; set; }
            public int IdleSeconds { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        // ==========================================
        // 1. LOGIN (Open to everyone)
        // ==========================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                // Log failed login attempt
                var failIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                await _logger.LogLogin("unknown", request.Username, "unknown", failIp);

                return Unauthorized(new { message = "Invalid username or password." });
            }

            await CloseTimedOutSessionsForUser(user.Id);

            // Generate the JWT Token
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(12),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            var now = NowStamp();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            var session = new UserSession
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                UserName = user.Name,
                UserRole = user.Role,
                LoginAt = now,
                LastSeenAt = now,
                IsActive = true,
                IpAddress = ip,
                UserAgent = userAgent
            };

            _context.UserSessions.Add(session);
            _context.UserSessionEvents.Add(CreateSessionEvent(session, "Login", 0, "User logged in"));
            await _context.SaveChangesAsync();

            // Log successful login in the existing ActivityLog as well
            await _logger.LogLogin(user.Id, user.Name, user.Role, ip);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                sessionId = session.Id,
                idleWarningSeconds = IdleWarningSeconds,
                idleLogoutGraceSeconds = IdleLogoutGraceSeconds,
                user = new { user.Id, user.Username, user.Name, user.Role }
            });
        }

        // ==========================================
        // HEARTBEAT: keeps LastSeenAt and active/idle totals updated
        // ==========================================
        [Authorize]
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
        {
            var sessionId = ResolveSessionId(request.SessionId);
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { message = "Session id is required." });

            var userId = CurrentUserId();
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                return NotFound(new { message = "Session was not found." });

            await CloseSessionIfTimedOut(session);
            if (!session.IsActive)
                return Ok(new { isActive = false, logoutType = session.LogoutType, logoutAt = session.LogoutAt });

            session.LastSeenAt = NowStamp();
            session.TotalActiveSeconds = Math.Max(session.TotalActiveSeconds, Math.Max(0, request.ActiveSeconds));
            session.TotalIdleSeconds = Math.Max(session.TotalIdleSeconds, Math.Max(0, request.IdleSeconds));
            session.TotalLoginSeconds = CalculateSeconds(session.LoginAt, session.LastSeenAt);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                isActive = true,
                session.Id,
                session.LastSeenAt,
                session.TotalLoginSeconds,
                session.TotalActiveSeconds,
                session.TotalIdleSeconds,
                idleWarningSeconds = IdleWarningSeconds,
                idleLogoutGraceSeconds = IdleLogoutGraceSeconds
            });
        }

        // ==========================================
        // SESSION EVENT: records warning/active-again markers without spamming heartbeats
        // ==========================================
        [Authorize]
        [HttpPost("session-event")]
        public async Task<IActionResult> SessionEvent([FromBody] SessionEventRequest request)
        {
            var sessionId = ResolveSessionId(request.SessionId);
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { message = "Session id is required." });

            var userId = CurrentUserId();
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                return NotFound(new { message = "Session was not found." });

            if (!session.IsActive)
                return Ok(new { isActive = false, logoutType = session.LogoutType, logoutAt = session.LogoutAt });

            session.LastSeenAt = NowStamp();
            session.TotalActiveSeconds = Math.Max(session.TotalActiveSeconds, Math.Max(0, request.ActiveSeconds));
            session.TotalIdleSeconds = Math.Max(session.TotalIdleSeconds, Math.Max(0, request.IdleSeconds));
            session.TotalLoginSeconds = CalculateSeconds(session.LoginAt, session.LastSeenAt);

            var eventType = string.IsNullOrWhiteSpace(request.EventType) ? "SessionEvent" : request.EventType.Trim();
            var description = string.IsNullOrWhiteSpace(request.Description) ? eventType : request.Description.Trim();
            _context.UserSessionEvents.Add(CreateSessionEvent(session, eventType, request.IdleSeconds, description));

            await _context.SaveChangesAsync();
            return Ok(new { isActive = true });
        }

        // ==========================================
        // LOGOUT: user/manual logout or system idle logout
        // ==========================================
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var sessionId = ResolveSessionId(request.SessionId);
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { message = "Session id is required." });

            var userId = CurrentUserId();
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                return Ok(new { message = "Session was already closed or not found." });

            session.TotalActiveSeconds = Math.Max(session.TotalActiveSeconds, Math.Max(0, request.ActiveSeconds));
            session.TotalIdleSeconds = Math.Max(session.TotalIdleSeconds, Math.Max(0, request.IdleSeconds));

            var logoutType = NormalizeLogoutType(request.LogoutType);
            var logoutReason = string.IsNullOrWhiteSpace(request.LogoutReason)
                ? (logoutType == "SystemIdle" ? "System idle logout" : "Manual logout")
                : request.LogoutReason.Trim();

            CloseSession(session, logoutType, logoutReason);
            _context.UserSessionEvents.Add(CreateSessionEvent(
                session,
                logoutType == "SystemIdle" ? "SystemIdleLogout" : "UserLogout",
                session.TotalIdleSeconds,
                logoutReason));

            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Logout", "Auth", session.Id,
                $"{session.UserName} logged out. Type: {session.LogoutType}. Login time: {FormatDuration(session.TotalLoginSeconds)}. Idle time: {FormatDuration(session.TotalIdleSeconds)}");

            return Ok(new
            {
                message = "Logged out successfully.",
                session.Id,
                session.LogoutAt,
                session.LogoutType,
                session.TotalLoginSeconds,
                session.TotalActiveSeconds,
                session.TotalIdleSeconds
            });
        }

        // ==========================================
        // TEMPORARY: SEED MASTER ADMIN
        // (Remove this before going to production!)
        // ==========================================
        [HttpPost("seed")]
        public async Task<IActionResult> SeedAdmin()
        {
            if (await _context.Users.AnyAsync(u => u.Role == "Admin"))
            {
                return BadRequest("An Admin already exists in the system.");
            }

            var masterAdmin = new Models.User
            {
                Id = Guid.NewGuid().ToString(),
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Name = "Master Administrator",
                Role = "Admin"
            };

            _context.Users.Add(masterAdmin);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Master Admin created! Username: admin | Password: Admin123!" });
        }

        // ==========================================
        // 2. REGISTER NEW EMPLOYEE (Admin Only)
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("Username is already taken.");
            }

            var newUser = new Models.User
            {
                Id = Guid.NewGuid().ToString(),
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Name = request.Name,
                Role = request.Role
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Log user creation
            await _logger.Log(User, HttpContext, "Create", "User", newUser.Id,
                $"Created user '{newUser.Name}' (@{newUser.Username}) with role {newUser.Role}");

            return Ok(new { message = "User registered successfully." });
        }

        private async Task CloseTimedOutSessionsForUser(string userId)
        {
            var openSessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync();

            foreach (var session in openSessions)
                await CloseSessionIfTimedOut(session);
        }

        private async Task CloseSessionIfTimedOut(UserSession session)
        {
            if (!session.IsActive) return;

            var lastSeen = ParseStamp(session.LastSeenAt);
            if (lastSeen == null) return;

            var secondsSinceLastSeen = (DateTime.UtcNow - lastSeen.Value).TotalSeconds;
            if (secondsSinceLastSeen < SessionTimeoutSeconds) return;

            CloseSession(session, "BrowserClosed", "Session closed because heartbeat stopped");
            _context.UserSessionEvents.Add(CreateSessionEvent(session, "SessionExpired", session.TotalIdleSeconds, "Session expired after missing heartbeat"));
            await _context.SaveChangesAsync();
        }

        private void CloseSession(UserSession session, string logoutType, string logoutReason)
        {
            var now = NowStamp();
            session.LastSeenAt = now;
            session.LogoutAt = now;
            session.LogoutType = logoutType;
            session.LogoutReason = logoutReason;
            session.IsActive = false;
            session.TotalLoginSeconds = CalculateSeconds(session.LoginAt, now);

            if (session.TotalActiveSeconds <= 0)
                session.TotalActiveSeconds = Math.Max(0, session.TotalLoginSeconds - session.TotalIdleSeconds);
        }

        private UserSessionEvent CreateSessionEvent(UserSession session, string eventType, int idleSeconds, string description)
        {
            return new UserSessionEvent
            {
                SessionId = session.Id,
                UserId = session.UserId,
                UserName = session.UserName,
                UserRole = session.UserRole,
                EventType = eventType,
                Timestamp = NowStamp(),
                IdleSeconds = Math.Max(0, idleSeconds),
                Description = description
            };
        }

        private string CurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? User.FindFirst("sub")?.Value
                   ?? "unknown";
        }

        private string ResolveSessionId(string? sessionId)
        {
            if (!string.IsNullOrWhiteSpace(sessionId)) return sessionId.Trim();
            return Request.Headers["X-Session-Id"].FirstOrDefault()?.Trim() ?? string.Empty;
        }

        private static string NormalizeLogoutType(string? logoutType)
        {
            var clean = logoutType?.Trim() ?? "";
            return clean.Equals("SystemIdle", StringComparison.OrdinalIgnoreCase) ? "SystemIdle"
                 : clean.Equals("TokenExpired", StringComparison.OrdinalIgnoreCase) ? "TokenExpired"
                 : clean.Equals("BrowserClosed", StringComparison.OrdinalIgnoreCase) ? "BrowserClosed"
                 : clean.Equals("Forced", StringComparison.OrdinalIgnoreCase) ? "Forced"
                 : "User";
        }

        private static string NowStamp() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        private static DateTime? ParseStamp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var parsed) ? parsed : null;
        }

        private static int CalculateSeconds(string? from, string? to)
        {
            var start = ParseStamp(from);
            var end = ParseStamp(to);
            if (start == null || end == null || end < start) return 0;
            return (int)Math.Round((end.Value - start.Value).TotalSeconds);
        }

        private static string FormatDuration(int totalSeconds)
        {
            var safeSeconds = Math.Max(0, totalSeconds);
            var ts = TimeSpan.FromSeconds(safeSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
