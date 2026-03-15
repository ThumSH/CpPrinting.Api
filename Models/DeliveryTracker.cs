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
        public string AdviceNoteId { get; set; } = string.Empty;
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

        public Dictionary<string, SizeData> SizeData { get; set; } = new();
    }

    public class DeliveryTrackerReport
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string AdviceNoteId { get; set; } = string.Empty;
        public string ProductionRecordId { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; } = 1;

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string AdNo { get; set; } = string.Empty;
        public string FpoNo { get; set; } = string.Empty;
        public int OrderQty { get; set; }
        public int DeliveryQty { get; set; }
        public int BalanceQty { get; set; }
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, In Transit, Delivered, Returned, Delayed
        public string CreatedAt { get; set; } = string.Empty;

        public List<DeliveryTrackerRow> Rows { get; set; } = new();
    }
}