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
    [Route("api/tax-invoices")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class TaxInvoicesController : ControllerBase
    {
        private const string SecuritySettingId = "invoice-security";

        private readonly AppDbContext _context;
        private readonly ActivityLogger _logger;

        public TaxInvoicesController(
            AppDbContext context,
            ActivityLogger logger)
        {
            _context = context;
            _logger = logger;
        }

        // Main Invoice page: only the latest 10 reports.
        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<TaxInvoiceSummaryDto>>>
            GetRecentInvoices()
        {
            var invoices = await _context.TaxInvoices
                .AsNoTracking()
                .OrderByDescending(invoice => invoice.CreatedAt)
                .Take(10)
                .Select(invoice => new TaxInvoiceSummaryDto
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    SupplierName = invoice.SupplierName,
                    PurchaserName = invoice.PurchaserName,
                    TotalAmountIncludingVat =
                        invoice.TotalAmountIncludingVat,
                    CreatedBy = invoice.CreatedBy,
                    CreatedAt = invoice.CreatedAt,
                    UpdatedAt = invoice.UpdatedAt
                })
                .ToListAsync();

            return Ok(invoices);
        }

        // Dropdown values used by the Invoice Search page.
        [HttpGet("filter-options")]
        public async Task<ActionResult<TaxInvoiceFilterOptionsDto>>
            GetFilterOptions()
        {
            var invoiceNumbers = await _context.TaxInvoices
                .AsNoTracking()
                .Where(invoice => invoice.InvoiceNumber != string.Empty)
                .Select(invoice => invoice.InvoiceNumber)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            var supplierNames = await _context.TaxInvoices
                .AsNoTracking()
                .Where(invoice => invoice.SupplierName != string.Empty)
                .Select(invoice => invoice.SupplierName)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            var purchaserNames = await _context.TaxInvoices
                .AsNoTracking()
                .Where(invoice => invoice.PurchaserName != string.Empty)
                .Select(invoice => invoice.PurchaserName)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            return Ok(new TaxInvoiceFilterOptionsDto
            {
                InvoiceNumbers = invoiceNumbers,
                SupplierNames = supplierNames,
                PurchaserNames = purchaserNames
            });
        }

        // Invoice Search page: paginated and filterable.
        [HttpGet("search")]
        public async Task<ActionResult<TaxInvoiceSearchResponseDto>>
            SearchInvoices(
                [FromQuery] string? invoiceNumber = null,
                [FromQuery] string? supplierName = null,
                [FromQuery] string? purchaserName = null,
                [FromQuery] string? supplierTin = null,
                [FromQuery] string? purchaserTin = null,
                [FromQuery] string? dateFrom = null,
                [FromQuery] string? dateTo = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 25)
        {
            if (page < 1)
                page = 1;

            if (pageSize < 1 || pageSize > 100)
                pageSize = 25;

            var query = _context.TaxInvoices
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                var value = invoiceNumber.Trim();
                query = query.Where(invoice =>
                    invoice.InvoiceNumber.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var value = supplierName.Trim();
                query = query.Where(invoice =>
                    invoice.SupplierName.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(purchaserName))
            {
                var value = purchaserName.Trim();
                query = query.Where(invoice =>
                    invoice.PurchaserName.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(supplierTin))
            {
                var value = supplierTin.Trim();
                query = query.Where(invoice =>
                    invoice.SupplierTin.Contains(value));
            }

            if (!string.IsNullOrWhiteSpace(purchaserTin))
            {
                var value = purchaserTin.Trim();
                query = query.Where(invoice =>
                    invoice.PurchaserTin.Contains(value));
            }

            // InvoiceDate is written by a date input as yyyy-MM-dd.
            // Lexicographical comparison is therefore date-safe.
            if (!string.IsNullOrWhiteSpace(dateFrom))
            {
                var value = dateFrom.Trim();
                query = query.Where(invoice =>
                    string.Compare(invoice.InvoiceDate, value) >= 0);
            }

            if (!string.IsNullOrWhiteSpace(dateTo))
            {
                var value = dateTo.Trim();
                query = query.Where(invoice =>
                    string.Compare(invoice.InvoiceDate, value) <= 0);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(invoice => invoice.InvoiceDate)
                .ThenByDescending(invoice => invoice.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(invoice => new TaxInvoiceSummaryDto
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    SupplierName = invoice.SupplierName,
                    PurchaserName = invoice.PurchaserName,
                    TotalAmountIncludingVat =
                        invoice.TotalAmountIncludingVat,
                    CreatedBy = invoice.CreatedBy,
                    CreatedAt = invoice.CreatedAt,
                    UpdatedAt = invoice.UpdatedAt
                })
                .ToListAsync();

            return Ok(new TaxInvoiceSearchResponseDto
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(
                    total / (double)pageSize
                )
            });
        }

        // View or print one complete invoice.
        [HttpGet("{id}")]
        public async Task<ActionResult<TaxInvoiceResponseDto>>
            GetInvoice(string id)
        {
            var invoice = await _context.TaxInvoices
                .AsNoTracking()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (invoice == null)
                return NotFound("Tax invoice was not found.");

            return Ok(ToResponse(invoice));
        }

        // Create does not require the invoice alteration password.
        [HttpPost]
        public async Task<ActionResult<TaxInvoiceResponseDto>>
            CreateInvoice(
                [FromBody] TaxInvoiceSaveRequestDto request)
        {
            var validationError = ValidateInvoice(request);

            if (validationError != null)
                return BadRequest(validationError);

            var invoiceNumber = request.InvoiceNumber.Trim();

            var duplicate = await _context.TaxInvoices
                .AnyAsync(invoice =>
                    invoice.InvoiceNumber == invoiceNumber);

            if (duplicate)
            {
                return BadRequest(
                    "A Tax Invoice with this invoice number already exists."
                );
            }

            var username = User.Identity?.Name ?? "unknown";

            var invoice = new TaxInvoice
            {
                Id = Guid.NewGuid().ToString(),
                InvoiceNumber = invoiceNumber,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            ApplyEditableFields(invoice, request);
            invoice.Items = BuildItems(invoice.Id, request.Items);

            _context.TaxInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            await _logger.Log(
                User,
                HttpContext,
                "Create",
                "TaxInvoice",
                invoice.Id,
                $"Created Tax Invoice {invoice.InvoiceNumber}"
            );

            return CreatedAtAction(
                nameof(GetInvoice),
                new { id = invoice.Id },
                ToResponse(invoice)
            );
        }

        // Allows the frontend to unlock Edit/Delete UI.
        // Update/Delete still verify the password again.
        [HttpPost("verify-password")]
        public async Task<ActionResult> VerifyInvoicePassword(
            [FromBody] InvoicePasswordRequestDto request)
        {
            var valid = await IsInvoicePasswordValid(
                request.Password
            );

            if (!valid)
            {
                return Unauthorized(new
                {
                    message = "Invalid invoice alteration password."
                });
            }

            return Ok(new { valid = true });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<TaxInvoiceResponseDto>>
            UpdateInvoice(
                string id,
                [FromBody] TaxInvoiceUpdateRequestDto request)
        {
            if (!await IsInvoicePasswordValid(
                    request.InvoicePassword))
            {
                return Unauthorized(new
                {
                    message = "Invalid invoice alteration password."
                });
            }

            var validationError = ValidateInvoice(request);

            if (validationError != null)
                return BadRequest(validationError);

            var invoice = await _context.TaxInvoices
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (invoice == null)
                return NotFound("Tax invoice was not found.");

            var invoiceNumber = request.InvoiceNumber.Trim();

            var duplicate = await _context.TaxInvoices
                .AnyAsync(item =>
                    item.InvoiceNumber == invoiceNumber &&
                    item.Id != id);

            if (duplicate)
            {
                return BadRequest(
                    "A different Tax Invoice already uses this invoice number."
                );
            }

            invoice.InvoiceNumber = invoiceNumber;
            ApplyEditableFields(invoice, request);

            // Replace rows so the stored order and manually entered values
            // exactly match the edited report.
            _context.TaxInvoiceItems.RemoveRange(invoice.Items);
            invoice.Items = BuildItems(invoice.Id, request.Items);

            invoice.UpdatedBy =
                User.Identity?.Name ?? "unknown";

            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _logger.Log(
                User,
                HttpContext,
                "Update",
                "TaxInvoice",
                invoice.Id,
                $"Updated Tax Invoice {invoice.InvoiceNumber}"
            );

            return Ok(ToResponse(invoice));
        }

        // Password stays in the JSON body and is never placed in the URL.
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteInvoice(
            string id,
            [FromBody] InvoicePasswordRequestDto request)
        {
            if (!await IsInvoicePasswordValid(request.Password))
            {
                return Unauthorized(new
                {
                    message = "Invalid invoice alteration password."
                });
            }

            var invoice = await _context.TaxInvoices
                .FirstOrDefaultAsync(item => item.Id == id);

            if (invoice == null)
                return NotFound("Tax invoice was not found.");

            var invoiceNumber = invoice.InvoiceNumber;

            _context.TaxInvoices.Remove(invoice);
            await _context.SaveChangesAsync();

            await _logger.Log(
                User,
                HttpContext,
                "Delete",
                "TaxInvoice",
                id,
                $"Deleted Tax Invoice {invoiceNumber}"
            );

            return Ok(new
            {
                message = "Tax invoice deleted successfully."
            });
        }

        private async Task<bool> IsInvoicePasswordValid(
            string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            var setting = await _context.InvoiceSecuritySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == SecuritySettingId);

            if (setting == null ||
                string.IsNullOrWhiteSpace(setting.PasswordHash))
            {
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(
                password,
                setting.PasswordHash
            );
        }

        private static string? ValidateInvoice(
            TaxInvoiceSaveRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(
                    request.InvoiceNumber))
            {
                return "Tax Invoice No. is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.InvoiceDate))
            {
                return "Date of Invoice is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.DeliveryDate))
            {
                return "Date of Delivery is required.";
            }

            if (request.Items == null)
            {
                return "Invoice item rows are required.";
            }

            return null;
        }

        private static void ApplyEditableFields(
            TaxInvoice invoice,
            TaxInvoiceSaveRequestDto request)
        {
            invoice.InvoiceDate =
                request.InvoiceDate.Trim();

            invoice.SupplierTin =
                request.SupplierTin.Trim();

            invoice.SupplierName =
                request.SupplierName.Trim();

            invoice.SupplierAddress =
                request.SupplierAddress.Trim();

            invoice.SupplierTelephone =
                request.SupplierTelephone.Trim();

            invoice.PurchaserTin =
                request.PurchaserTin.Trim();

            invoice.PurchaserName =
                request.PurchaserName.Trim();

            invoice.PurchaserAddress =
                request.PurchaserAddress.Trim();

            invoice.PurchaserTelephone =
                request.PurchaserTelephone.Trim();

            invoice.DeliveryDate =
                request.DeliveryDate.Trim();

            invoice.PlaceOfSupply =
                request.PlaceOfSupply.Trim();

            invoice.AdditionalInformation =
                request.AdditionalInformation.Trim();

            invoice.TotalValueOfSupply =
                request.TotalValueOfSupply.Trim();

            invoice.VatAmount =
                request.VatAmount.Trim();

            invoice.TotalAmountIncludingVat =
                request.TotalAmountIncludingVat.Trim();

            invoice.TotalAmountInWords =
                request.TotalAmountInWords.Trim();

            invoice.ModeOfPayment =
                request.ModeOfPayment.Trim();
        }

        private static List<TaxInvoiceItem> BuildItems(
            string invoiceId,
            IEnumerable<TaxInvoiceItemRequestDto>? items)
        {
            return (items ?? Array.Empty<TaxInvoiceItemRequestDto>())
                .Select((item, index) => new TaxInvoiceItem
                {
                    Id = Guid.NewGuid().ToString(),
                    TaxInvoiceId = invoiceId,
                    RowOrder = index + 1,
                    Reference = item.Reference.Trim(),
                    Description = item.Description.Trim(),
                    Quantity = item.Quantity.Trim(),
                    UnitPrice = item.UnitPrice.Trim(),
                    AmountExcludingVat =
                        item.AmountExcludingVat.Trim()
                })
                .ToList();
        }

        private static TaxInvoiceResponseDto ToResponse(
            TaxInvoice invoice)
        {
            return new TaxInvoiceResponseDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,

                SupplierTin = invoice.SupplierTin,
                SupplierName = invoice.SupplierName,
                SupplierAddress = invoice.SupplierAddress,
                SupplierTelephone = invoice.SupplierTelephone,

                PurchaserTin = invoice.PurchaserTin,
                PurchaserName = invoice.PurchaserName,
                PurchaserAddress = invoice.PurchaserAddress,
                PurchaserTelephone = invoice.PurchaserTelephone,

                DeliveryDate = invoice.DeliveryDate,
                PlaceOfSupply = invoice.PlaceOfSupply,
                AdditionalInformation =
                    invoice.AdditionalInformation,

                TotalValueOfSupply =
                    invoice.TotalValueOfSupply,

                VatAmount = invoice.VatAmount,

                TotalAmountIncludingVat =
                    invoice.TotalAmountIncludingVat,

                TotalAmountInWords =
                    invoice.TotalAmountInWords,

                ModeOfPayment = invoice.ModeOfPayment,

                CreatedBy = invoice.CreatedBy,
                CreatedAt = invoice.CreatedAt,
                UpdatedBy = invoice.UpdatedBy,
                UpdatedAt = invoice.UpdatedAt,

                Items = invoice.Items
                    .OrderBy(item => item.RowOrder)
                    .Select(item => new TaxInvoiceItemResponseDto
                    {
                        Id = item.Id,
                        RowOrder = item.RowOrder,
                        Reference = item.Reference,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        AmountExcludingVat =
                            item.AmountExcludingVat
                    })
                    .ToList()
            };
        }
    }
}