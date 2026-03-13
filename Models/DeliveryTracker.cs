using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class SizeData
    {
        public int Qty { get; set; }
        public int Pd { get; set; } // Print Defect
        public int Fd { get; set; } // Fabric Defect
    }

    public class DeliveryTrackerRow
    {
        public string Id { get; set; } = string.Empty;
        public string InDate { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        public string InAd { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Schedule { get; set; } = string.Empty;
        public int FpoQty { get; set; }
        public int AllowedPd { get; set; }
        public string CutNo { get; set; } = string.Empty;
        
        // Deeply nested dictionary
        public Dictionary<string, SizeData> SizeData { get; set; } = new();
    }

    public class DeliveryTrackerReport
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string FpoNo { get; set; } = string.Empty;
        public int OrderQty { get; set; }
        public string CreatedAt { get; set; } = string.Empty;

        // The massive array of rows
        public List<DeliveryTrackerRow> Rows { get; set; } = new();
    }
}