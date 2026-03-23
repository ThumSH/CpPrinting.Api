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

            // Log successful login
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _logger.LogLogin(user.Id, user.Name, user.Role, ip);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                user = new { user.Id, user.Username, user.Name, user.Role }
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
    }
}