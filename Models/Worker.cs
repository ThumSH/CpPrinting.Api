using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class TimeSlotEntry
    {
        public string TimeFrom { get; set; } = string.Empty;
        public string TimeTo { get; set; } = string.Empty;
        public int Seating { get; set; }
        public int Printing { get; set; }
        public int Curing { get; set; }
        public int Checking { get; set; }
        public int Packing { get; set; }
        public int Dispatch { get; set; }
    }

    public class DailyOutputRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public int OrderQty { get; set; }
        public string TableNo { get; set; } = string.Empty;
        public int Target { get; set; }
        public int DailyTarget { get; set; }
        public List<TimeSlotEntry> TimeSlots { get; set; } = new();
        public int TotalSeating { get; set; }
        public int TotalPrinting { get; set; }
        public int TotalCuring { get; set; }
        public int TotalChecking { get; set; }
        public int TotalPacking { get; set; }
        public int TotalDispatch { get; set; }
        public string WorkerName { get; set; } = string.Empty;
    }
}