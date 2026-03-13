using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class StoreInRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        [Required]
        public string StyleNo { get; set; } = string.Empty;
        [Required]
        public string ScheduleNo { get; set; } = string.Empty;
        [Required]
        public string CutNo { get; set; } = string.Empty;
        
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string CutInDate { get; set; } = string.Empty;
        
        // Quantity Waterfall
        public int BulkQty { get; set; }
        public int InQty { get; set; }
        public int BalanceBulkQty { get; set; }
        public int CutQty { get; set; }
        public int AvailableQty { get; set; }
        
        public int BundleQty { get; set; }
        public string NumberRange { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
    }

    public class StoreProductionRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string IssueDate { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public int IssueQty { get; set; }
        public int BalanceQty { get; set; } 
        public string LineNo { get; set; } = string.Empty;
    }
}