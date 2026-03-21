using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class AdviceNoteRow
    {
        public string ProductionRecordId { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        public string BundleNo { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string CutForm { get; set; } = string.Empty;
        public int TotalPcs { get; set; }
        public int Pd { get; set; }
        public int Fd { get; set; }
        public int GoodQty { get; set; }
    }

    public class AdviceNoteRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string ProductionRecordId { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; } = 1;

        public string AdNo { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string Attn { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;

        public int DispatchQty { get; set; }
        public int BalanceQty { get; set; }

        // Dynamic grid — per-bundle rows stored as JSON
        public Dictionary<string, AdviceNoteRow> Rows { get; set; } = new();

        // Footer
        public string ReceivedByName { get; set; } = string.Empty;
        public string PrepByName { get; set; } = string.Empty;
        public string AuthByName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}