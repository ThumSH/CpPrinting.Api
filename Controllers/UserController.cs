using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CpPrinting.Api.Data;
using CpPrinting.Api.DTOs;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;

namespace CpPrinting.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        private static readonly string[] AllowedRoles =
        {
            "Admin", "Developer", "QC", "Gatepass", "Audit", "Stores", "Worker"
        };

        public UsersController(AppDbContext context, ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
        {
            var users = await _context.Users
                .OrderBy(u => u.Name)
                .Select(u => new UserResponseDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Name = u.Name,
                    Role = u.Role
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserResponseDto>> GetUser(string id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Name = user.Name,
                Role = user.Role
            });
        }

        [HttpPost]
        public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest("Username is required.");

            if (string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Password is required.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Name is required.");

            if (string.IsNullOrWhiteSpace(dto.Role))
                return BadRequest("Role is required.");

            if (!AllowedRoles.Contains(dto.Role))
                return BadRequest("Invalid role.");

            var usernameExists = await _context.Users.AnyAsync(u => u.Username == dto.Username);
            if (usernameExists)
                return BadRequest("Username already exists.");

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = dto.Username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Name = dto.Name.Trim(),
                Role = dto.Role
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Create", "User", newUser.Id,
                $"Created user '{newUser.Name}' (@{newUser.Username}) — {newUser.Role}");

            return Ok(new UserResponseDto
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Name = newUser.Name,
                Role = newUser.Role
            });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserResponseDto>> UpdateUser(string id, UpdateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest("Username is required.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Name is required.");

            if (string.IsNullOrWhiteSpace(dto.Role))
                return BadRequest("Role is required.");

            if (!AllowedRoles.Contains(dto.Role))
                return BadRequest("Invalid role.");

            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            var usernameTaken = await _context.Users.AnyAsync(u => u.Username == dto.Username && u.Id != id);
            if (usernameTaken)
                return BadRequest("Username already exists.");

            var currentUsername = User.Identity?.Name;

            if (user.Username == currentUsername && dto.Role != "Admin")
            {
                return BadRequest("You cannot remove your own Admin role.");
            }

            var oldRole = user.Role;
            user.Username = dto.Username.Trim();
            user.Name = dto.Name.Trim();
            user.Role = dto.Role;

            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Update", "User", id,
                $"Updated user '{user.Name}' (@{user.Username})" +
                (oldRole != dto.Role ? $" — role changed {oldRole} → {dto.Role}" : ""));

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Name = user.Name,
                Role = user.Role
            });
        }

        [HttpPatch("{id}/password")]
        public async Task<ActionResult> ResetPassword(string id, ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest("New password is required.");

            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Update", "User", id,
                $"Reset password for '{user.Name}' (@{user.Username})");

            return Ok(new { message = "Password reset successfully." });
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(string id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound("User not found.");

            var currentUsername = User.Identity?.Name;

            if (user.Username == currentUsername)
            {
                return BadRequest("You cannot delete your own account.");
            }

            var userName = user.Name;
            var userRole = user.Role;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            await _logger.Log(User, HttpContext, "Delete", "User", id,
                $"Deleted user '{userName}' ({userRole})");

            return Ok(new { message = "User deleted successfully." });
        }
    }
}