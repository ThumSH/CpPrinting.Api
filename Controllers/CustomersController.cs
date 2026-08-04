using System.ComponentModel.DataAnnotations;
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
    [Route("api/customers")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public CustomersController(
            AppDbContext context,
            ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        // Used by the Tax Invoice purchaser dropdown
        // and the customer administration page.
        [HttpGet]
        public async Task<
            ActionResult<IEnumerable<CustomerResponseDto>>>
            GetCustomers()
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .OrderBy(customer =>
                    customer.CustomerName)
                .ThenBy(customer =>
                    customer.CustomerCode)
                .Select(customer =>
                    new CustomerResponseDto
                    {
                        Id = customer.Id,
                        CustomerName =
                            customer.CustomerName,
                        CustomerCode =
                            customer.CustomerCode,
                        Address =
                            customer.Address,
                        TinNumber =
                            customer.TinNumber,
                        TelephoneNumber =
                            customer.TelephoneNumber,
                        Email =
                            customer.Email,
                        CreatedBy =
                            customer.CreatedBy,
                        CreatedAt =
                            customer.CreatedAt,
                        UpdatedBy =
                            customer.UpdatedBy,
                        UpdatedAt =
                            customer.UpdatedAt
                    })
                .ToListAsync();

            return Ok(customers);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerResponseDto>>
            GetCustomer(string id)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(customer =>
                    customer.Id == id);

            if (customer == null)
            {
                return NotFound(
                    "The customer could not be found."
                );
            }

            return Ok(ToResponse(customer));
        }

        // Customer registration is restricted to Admin.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CustomerResponseDto>>
            CreateCustomer(
                [FromBody] CustomerSaveRequestDto request)
        {
            var validationError =
                ValidateCustomer(request);

            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var customerCode =
                NormaliseCustomerCode(
                    request.CustomerCode
                );

            var duplicateCode =
                await _context.Customers
                    .AnyAsync(customer =>
                        customer.CustomerCode ==
                        customerCode);

            if (duplicateCode)
            {
                return BadRequest(
                    "A customer with this customer code already exists."
                );
            }

            var username =
                User.Identity?.Name ?? "unknown";

            var customer = new Customer
            {
                Id = Guid.NewGuid().ToString(),

                CustomerName =
                    request.CustomerName.Trim(),

                CustomerCode =
                    customerCode,

                Address =
                    request.Address.Trim(),

                TinNumber =
                    request.TinNumber.Trim(),

                TelephoneNumber =
                    request.TelephoneNumber.Trim(),

                Email =
                    request.Email.Trim(),

                CreatedBy =
                    username,

                CreatedAt =
                    DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _logger.Log(
                User,
                HttpContext,
                "Create",
                "Customer",
                customer.Id,
                $"Registered customer " +
                $"{customer.CustomerCode} - " +
                $"{customer.CustomerName}"
            );

            return CreatedAtAction(
                nameof(GetCustomer),
                new { id = customer.Id },
                ToResponse(customer)
            );
        }

        // Allows Admin to correct registered customer
        // details without changing historical invoices.
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CustomerResponseDto>>
            UpdateCustomer(
                string id,
                [FromBody] CustomerSaveRequestDto request)
        {
            var validationError =
                ValidateCustomer(request);

            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var customer =
                await _context.Customers
                    .FirstOrDefaultAsync(item =>
                        item.Id == id);

            if (customer == null)
            {
                return NotFound(
                    "The customer could not be found."
                );
            }

            var customerCode =
                NormaliseCustomerCode(
                    request.CustomerCode
                );

            var duplicateCode =
                await _context.Customers
                    .AnyAsync(item =>
                        item.CustomerCode ==
                            customerCode &&
                        item.Id != id);

            if (duplicateCode)
            {
                return BadRequest(
                    "A different customer already uses this customer code."
                );
            }

            customer.CustomerName =
                request.CustomerName.Trim();

            customer.CustomerCode =
                customerCode;

            customer.Address =
                request.Address.Trim();

            customer.TinNumber =
                request.TinNumber.Trim();

            customer.TelephoneNumber =
                request.TelephoneNumber.Trim();

            customer.Email =
                request.Email.Trim();

            customer.UpdatedBy =
                User.Identity?.Name ?? "unknown";

            customer.UpdatedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _logger.Log(
                User,
                HttpContext,
                "Update",
                "Customer",
                customer.Id,
                $"Updated customer " +
                $"{customer.CustomerCode} - " +
                $"{customer.CustomerName}"
            );

            return Ok(ToResponse(customer));
        }

        private static string? ValidateCustomer(
            CustomerSaveRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(
                    request.CustomerName))
            {
                return "Customer name is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.CustomerCode))
            {
                return "Customer code is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.Address))
            {
                return "Customer address is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.TinNumber))
            {
                return "TIN number is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.TelephoneNumber))
            {
                return "Telephone number is required.";
            }

            if (!string.IsNullOrWhiteSpace(
                    request.Email))
            {
                var emailValidator =
                    new EmailAddressAttribute();

                if (!emailValidator.IsValid(
                        request.Email.Trim()))
                {
                    return
                        "Please enter a valid email address.";
                }
            }

            return null;
        }

        private static string NormaliseCustomerCode(
            string value)
        {
            return value
                .Trim()
                .ToUpperInvariant();
        }

        private static CustomerResponseDto ToResponse(
            Customer customer)
        {
            return new CustomerResponseDto
            {
                Id =
                    customer.Id,

                CustomerName =
                    customer.CustomerName,

                CustomerCode =
                    customer.CustomerCode,

                Address =
                    customer.Address,

                TinNumber =
                    customer.TinNumber,

                TelephoneNumber =
                    customer.TelephoneNumber,

                Email =
                    customer.Email,

                CreatedBy =
                    customer.CreatedBy,

                CreatedAt =
                    customer.CreatedAt,

                UpdatedBy =
                    customer.UpdatedBy,

                UpdatedAt =
                    customer.UpdatedAt
            };
        }
    }
}