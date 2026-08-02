using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class TaxInvoice
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
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

        // Existing amount columns remain strings for backwards compatibility.
        // New invoices are calculated by the client and verified by the API.
        public string VatPercentage { get; set; } = "18";
        public string TotalValueOfSupply { get; set; } = string.Empty;
        public string VatAmount { get; set; } = string.Empty;
        public string TotalAmountIncludingVat { get; set; } = string.Empty;
        public string TotalAmountInWords { get; set; } = string.Empty;
        public string ModeOfPayment { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }

        public List<TaxInvoiceItem> Items { get; set; } = new();
    }
}
