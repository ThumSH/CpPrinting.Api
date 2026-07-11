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
        public bool Submitted { get; set; } = false;
    }

    public class DailyOutputRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        // Links
        public string StoreInRecordId { get; set; } = string.Empty;

        /// <summary>
        /// Production record ID — uniquely identifies which production row this daily output
        /// belongs to. One production record = one cut + one component + one line.
        /// </summary>
        public string ProductionRecordId { get; set; } = string.Empty;

        /// <summary>
        /// Manual completion marker used when a worker/admin closes a production job
        /// even if one or more stages still have remaining quantities.
        /// Completion marker rows are excluded from normal daily output listings.
        /// </summary>
        public bool IsJobCompleted { get; set; } = false;
        public string CompletedAt { get; set; } = string.Empty;
        public string CompletedBy { get; set; } = string.Empty;

        public string SubmissionId { get; set; } = string.Empty;

        // Metadata
        public string Date { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// The specific cut this daily output is working on (e.g. "c01", "c02").
        /// Needed to correctly match against production records.
        /// </summary>
        public string CutNo { get; set; } = string.Empty;

        public string Component { get; set; } = string.Empty;

        public int OrderQty { get; set; }
        public string TableNo { get; set; } = string.Empty;
        public int Target { get; set; }
        public int DailyTarget { get; set; }

        public List<TimeSlotEntry> TimeSlots { get; set; } = new();

        // Aggregated totals
        public int TotalSeating { get; set; }
        public int TotalPrinting { get; set; }
        public int TotalCuring { get; set; }
        public int TotalChecking { get; set; }
        public int TotalPacking { get; set; }
        public int TotalDispatch { get; set; }

        public string WorkerName { get; set; } = string.Empty;
    }
}