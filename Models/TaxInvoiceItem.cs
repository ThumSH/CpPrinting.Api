using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CpPrinting.Api.Models
{
    public class TaxInvoiceItem
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string TaxInvoiceId { get; set; } = string.Empty;

        [ForeignKey(nameof(TaxInvoiceId))]
        public TaxInvoice? TaxInvoice { get; set; }

        // Preserves the exact manual row order entered on the invoice.
        public int RowOrder { get; set; }

        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Manual values: no automatic calculations are performed.
        public string Quantity { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string AmountExcludingVat { get; set; } = string.Empty;
    }
}