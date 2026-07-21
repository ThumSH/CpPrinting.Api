using CpPrinting.Api.Data;
using CpPrinting.Api.DTOs;
using CpPrinting.Api.Models;
using CpPrinting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CpPrinting.Api.Controllers
{
    [ApiController]
    [Route("api/invoice-security")]
    [Authorize(Roles = "SuperAdmin")]
    public class InvoiceSecurityController : ControllerBase
    {
        private const string SecuritySettingId =
            "invoice-security";

        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public InvoiceSecurityController(
            AppDbContext context,
            ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<ActionResult> GetStatus()
        {
            var setting = await _context
                .InvoiceSecuritySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == SecuritySettingId);

            return Ok(new
            {
                hasPassword =
                    setting != null &&
                    !string.IsNullOrWhiteSpace(
                        setting.PasswordHash
                    ),

                updatedBy =
                    setting?.UpdatedBy ?? string.Empty,

                updatedAt = setting?.UpdatedAt
            });
        }

        [HttpPut("password")]
        public async Task<ActionResult> SetPassword(
            [FromBody] SetInvoicePasswordRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(
                    "Invoice alteration password is required."
                );
            }

            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(
                    "Password and confirmation do not match."
                );
            }

            if (request.Password.Length < 4)
            {
                return BadRequest(
                    "Invoice alteration password must contain at least 4 characters."
                );
            }

            var setting = await _context
                .InvoiceSecuritySettings
                .FirstOrDefaultAsync(item =>
                    item.Id == SecuritySettingId);

            if (setting == null)
            {
                setting = new InvoiceSecuritySetting
                {
                    Id = SecuritySettingId
                };

                _context.InvoiceSecuritySettings.Add(setting);
            }

            setting.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.Password
                );

            setting.UpdatedBy =
                User.Identity?.Name ?? "unknown";

            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _logger.Log(
                User,
                HttpContext,
                "Update",
                "InvoiceSecuritySetting",
                setting.Id,
                "Updated the Tax Invoice alteration password"
            );

            return Ok(new
            {
                message =
                    "Invoice alteration password saved successfully.",

                hasPassword = true,
                setting.UpdatedBy,
                setting.UpdatedAt
            });
        }
    }
}