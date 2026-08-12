namespace CpPrinting.Api.DTOs
{
    public class TaxInvoiceItemRequestDto
    {
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string AmountExcludingVat { get; set; } = string.Empty;
    }

    public class TaxInvoiceSaveRequestDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;

        public string SupplierTin { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierAddress { get; set; } = string.Empty;
        public string SupplierTelephone { get; set; } = string.Empty;

        public string PurchaserTin { get; set; } = string.Empty;
        public string PurchaserName { get; set; } = string.Empty;
        public string PurchaserAddress { get; set; } = string.Empty;
        public string PurchaserTelephone { get; set; } = string.Empty;

        public string DeliveryDate { get; set; } = string.Empty;
        public string PlaceOfSupply { get; set; } = string.Empty;
        public string AdditionalInformation { get; set; } = string.Empty;

        // Older clients that do not send this property safely continue at 18%.
        public string VatPercentage { get; set; } = "18";

        // Retained for API compatibility. The backend recalculates these values.
        public string TotalValueOfSupply { get; set; } = string.Empty;
        public string VatAmount { get; set; } = string.Empty;
        public string TotalAmountIncludingVat { get; set; } = string.Empty;

        public string ExchangeRate { get; set; } =
    string.Empty;

            public string TotalValueOfSupplyLkr { get; set; } =
                string.Empty;

            public string VatAmountLkr { get; set; } =
                string.Empty;

            public string TotalAmountIncludingVatLkr { get; set; } =
                string.Empty;

        public string TotalAmountInWords { get; set; } = string.Empty;
        public string ModeOfPayment { get; set; } = string.Empty;

        public List<TaxInvoiceItemRequestDto> Items { get; set; } = new();
    }

    public class TaxInvoiceUpdateRequestDto : TaxInvoiceSaveRequestDto
    {
        public string InvoicePassword { get; set; } = string.Empty;
    }

    public class InvoicePasswordRequestDto
    {
        public string Password { get; set; } = string.Empty;
    }

    public class SetInvoicePasswordRequestDto
    {
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class TaxInvoiceItemResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public int RowOrder { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string AmountExcludingVat { get; set; } = string.Empty;
    }

    public class TaxInvoiceResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;

        public string SupplierTin { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierAddress { get; set; } = string.Empty;
        public string SupplierTelephone { get; set; } = string.Empty;

        public string PurchaserTin { get; set; } = string.Empty;
        public string PurchaserName { get; set; } = string.Empty;
        public string PurchaserAddress { get; set; } = string.Empty;
        public string PurchaserTelephone { get; set; } = string.Empty;

        public string DeliveryDate { get; set; } = string.Empty;
        public string PlaceOfSupply { get; set; } = string.Empty;
        public string AdditionalInformation { get; set; } = string.Empty;

        public string VatPercentage { get; set; } = "18";
        public string TotalValueOfSupply { get; set; } = string.Empty;
        public string VatAmount { get; set; } = string.Empty;
        public string TotalAmountIncludingVat { get; set; } = string.Empty;

        public string ExchangeRate { get; set; } =
    string.Empty;

public string TotalValueOfSupplyLkr { get; set; } =
    string.Empty;

public string VatAmountLkr { get; set; } =
    string.Empty;

public string TotalAmountIncludingVatLkr { get; set; } =
    string.Empty;
    
        public string TotalAmountInWords { get; set; } = string.Empty;
        public string ModeOfPayment { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }

        public List<TaxInvoiceItemResponseDto> Items { get; set; } = new();
    }

    public class TaxInvoiceSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string PurchaserName { get; set; } = string.Empty;
        public string TotalAmountIncludingVat { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TaxInvoiceFilterOptionsDto
    {
        public List<string> InvoiceNumbers { get; set; } = new();
        public List<string> SupplierNames { get; set; } = new();
        public List<string> PurchaserNames { get; set; } = new();
    }

    public class TaxInvoiceSearchResponseDto
    {
        public List<TaxInvoiceSummaryDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
