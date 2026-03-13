using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class AuditBundle
    {
        public string Id { get; set; } = string.Empty;
        public string BundleNo { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public int Qty { get; set; }
    }

    public class AuditRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        
        // Complex Array - we will tell EF Core to store this as JSON
        public List<AuditBundle> Bundles { get; set; } = new();

        public string Sizes { get; set; } = string.Empty;      
        public int TotalQty { get; set; }   
        public int AuditQty { get; set; }   
        public string Status { get; set; } = string.Empty; // Pending, Pass, Fail
        public string Remarks { get; set; } = string.Empty;
    }
}