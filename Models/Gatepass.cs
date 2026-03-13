using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class AdviceNoteRow
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public int Pd { get; set; } // Print Defect / Panel Defect
        public int Fd { get; set; } // Fabric Defect
        public int GoodQty { get; set; }
    }

    public class AdviceNoteRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string AdNo { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string Attn { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        
        // Dynamic Grid - Will be converted to JSON in MS SQL
        public Dictionary<string, AdviceNoteRow> Rows { get; set; } = new();
        
        // Footer fields
        public string ReceivedByName { get; set; } = string.Empty;
        public string PrepByName { get; set; } = string.Empty;
        public string AuthByName { get; set; } = string.Empty;
    }
}