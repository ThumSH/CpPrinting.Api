using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class DowntimeEntry
    {
        public string Type { get; set; } = string.Empty;
        public double Hours { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string TimeFrom { get; set; } = string.Empty;
        public string TimeTo { get; set; } = string.Empty;
        public bool IsAcknowledged { get; set; } = false;
        public string AcknowledgedBy { get; set; } = string.Empty;
    }

    public class DowntimeRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string TableNo { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public List<DowntimeEntry> Entries { get; set; } = new();
        public double TotalHours { get; set; }
        public bool FullyAcknowledged { get; set; } = false;
    }
}