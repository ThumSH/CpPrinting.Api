using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CpPrinting.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CpPrinting.Api.Models;

namespace CpPrinting.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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

            // 1. Check if user exists AND if the password matches the BCrypt hash
           if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            // 2. Generate the JWT Token
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
            // Check if an admin already exists so we don't accidentally make duplicates
            if (await _context.Users.AnyAsync(u => u.Role == "Admin"))
            {
                return BadRequest("An Admin already exists in the system.");
            }

            var masterAdmin = new Models.User
            {
                Id = Guid.NewGuid().ToString(),
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), // The secure hash is generated here
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
        [Authorize(Roles = "Admin")] // CRITICAL: Only Admins can create new user accounts
        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
        {
            // Check if username is already taken
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("Username is already taken.");
            }

            // Create the new user and HASH the password immediately
            var newUser = new Models.User
            {
                Id = Guid.NewGuid().ToString(), // Generate a unique secure ID
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), // Scramble it!
                Name = request.Name,
                Role = request.Role
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully." });
        }
    }
}