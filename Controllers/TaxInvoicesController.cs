using System.Globalization;
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

        private const string DefaultModeOfPayment =
        "CREDIT - NET 30 DAYS";

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

            invoice.Items = BuildItems(
                invoice.Id,
                request.Items
            );

            ApplyEditableFields(
                invoice,
                request,
                invoice.Items
            );

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

            // Replace rows so the stored order exactly matches the edited report.
            _context.TaxInvoiceItems.RemoveRange(invoice.Items);

            invoice.Items = BuildItems(
                invoice.Id,
                request.Items
            );

            ApplyEditableFields(
                invoice,
                request,
                invoice.Items
            );

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

            if (!DateOnly.TryParseExact(
                    Clean(request.InvoiceDate),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                return "Date of Invoice must use MM/DD/YY in the form.";
            }

            if (string.IsNullOrWhiteSpace(
                    request.DeliveryDate))
            {
                return "Date of Delivery is required.";
            }

            if (!DateOnly.TryParseExact(
                    Clean(request.DeliveryDate),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                return "Date of Delivery must use MM/DD/YY in the form.";
            }

            if (request.Items == null)
            {
                return "Invoice item rows are required.";
            }

            if (!TryParseVatPercentage(
                    request.VatPercentage,
                    out _))
            {
                return "VAT percentage must be a number from 0 to 100.";
            }

            var itemIndex = 0;

            foreach (var item in request.Items)
            {
                itemIndex += 1;

                var quantity = Clean(item.Quantity);
                var unitPrice = Clean(item.UnitPrice);

                var hasQuantity =
                    !string.IsNullOrWhiteSpace(quantity);

                var hasUnitPrice =
                    !string.IsNullOrWhiteSpace(unitPrice);

                if (!hasQuantity && !hasUnitPrice)
                {
                    continue;
                }

                if (!hasQuantity || !hasUnitPrice)
                {
                    return
                        $"Invoice row {itemIndex} requires both quantity and unit price.";
                }

                if (!TryParseNonNegativeDecimal(
                        quantity,
                        out _))
                {
                    return
                        $"Invoice row {itemIndex} has an invalid quantity.";
                }

                if (!TryParseNonNegativeDecimal(
                        unitPrice,
                        out _))
                {
                    return
                        $"Invoice row {itemIndex} has an invalid unit price.";
                }
            }

            return null;
        }

        private static void ApplyEditableFields(
            TaxInvoice invoice,
            TaxInvoiceSaveRequestDto request,
            IReadOnlyCollection<TaxInvoiceItem> items)
        {
            invoice.InvoiceDate =
                Clean(request.InvoiceDate);

            invoice.SupplierTin =
                Clean(request.SupplierTin);

            invoice.SupplierName =
                Clean(request.SupplierName);

            invoice.SupplierAddress =
                Clean(request.SupplierAddress);

            invoice.SupplierTelephone =
                Clean(request.SupplierTelephone);

            invoice.PurchaserTin =
                Clean(request.PurchaserTin);

            invoice.PurchaserName =
                Clean(request.PurchaserName);

            invoice.PurchaserAddress =
                Clean(request.PurchaserAddress);

            invoice.PurchaserTelephone =
                Clean(request.PurchaserTelephone);

            invoice.DeliveryDate =
                Clean(request.DeliveryDate);

           // Place of Supply always follows the supplier name.
            invoice.PlaceOfSupply =
                invoice.SupplierName;

            invoice.AdditionalInformation =
                Clean(request.AdditionalInformation);

            TryParseVatPercentage(
                request.VatPercentage,
                out var vatPercentage
            );

            var totalValue = items
                .Sum(item =>
                    ParseStoredAmount(
                        item.AmountExcludingVat
                    )
                );

            totalValue = decimal.Round(
                totalValue,
                2,
                MidpointRounding.AwayFromZero
            );

            var vatAmount = decimal.Round(
                totalValue *
                    (vatPercentage / 100m),
                0,
                MidpointRounding.AwayFromZero
            );

            var totalIncludingVat =
                decimal.Round(
                    totalValue + vatAmount,
                    2,
                    MidpointRounding.AwayFromZero
                );

            invoice.VatPercentage =
                FormatPercentage(vatPercentage);

            invoice.TotalValueOfSupply =
                FormatMoney(totalValue);

            invoice.VatAmount =
                 FormatMoney(vatAmount);

            invoice.TotalAmountIncludingVat =
                FormatMoney(totalIncludingVat);

            invoice.TotalAmountInWords =
                Clean(request.TotalAmountInWords)
                    .ToUpperInvariant();

            invoice.ModeOfPayment =
                DefaultModeOfPayment;
        }

        private static List<TaxInvoiceItem> BuildItems(
            string invoiceId,
            IEnumerable<TaxInvoiceItemRequestDto>? items)
        {
            return (
                items ??
                Array.Empty<TaxInvoiceItemRequestDto>()
            )
            .Select((item, index) =>
            {
                var quantity =
                    Clean(item.Quantity);

                var unitPrice =
                    Clean(item.UnitPrice);

                var amount =
                    string.IsNullOrWhiteSpace(quantity) &&
                    string.IsNullOrWhiteSpace(unitPrice)
                        ? string.Empty
                        : FormatMoney(
                            ParseStoredAmount(quantity) *
                            ParseStoredAmount(unitPrice)
                        );

                return new TaxInvoiceItem
                {
                    Id = Guid.NewGuid().ToString(),
                    TaxInvoiceId = invoiceId,
                    RowOrder = index + 1,
                    Reference =
                        Clean(item.Reference),
                    Description =
                        Clean(item.Description),
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    AmountExcludingVat = amount
                };
            })
            .ToList();
        }

        private static bool TryParseVatPercentage(
            string? value,
            out decimal percentage)
        {
            var cleaned = Clean(value);

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                percentage = 18m;
                return true;
            }

            if (!TryParseNonNegativeDecimal(
                    cleaned,
                    out percentage))
            {
                return false;
            }

            return percentage <= 100m;
        }

        private static bool TryParseNonNegativeDecimal(
            string? value,
            out decimal parsed)
        {
            var cleaned = Clean(value);

            var valid = decimal.TryParse(
                cleaned,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsed
            );

            return valid && parsed >= 0m;
        }

        private static decimal ParseStoredAmount(
            string? value)
        {
            return decimal.TryParse(
                Clean(value),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
                : 0m;
        }

        private static string FormatMoney(
            decimal value)
        {
            return decimal.Round(
                    value,
                    2,
                    MidpointRounding.AwayFromZero
                )
                .ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                );
        }

        private static string FormatPercentage(
            decimal value)
        {
            return value.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            );
        }

        private static string Clean(
            string? value)
        {
            return value?.Trim() ?? string.Empty;
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

                VatPercentage =
                    string.IsNullOrWhiteSpace(
                        invoice.VatPercentage)
                        ? "18"
                        : invoice.VatPercentage,

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