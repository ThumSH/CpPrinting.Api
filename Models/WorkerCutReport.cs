using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// Saved Worker cut/bundle status report.
    /// This is reporting/process-tracking data only and does not update Store-In,
    /// Gatepass, QC, inventory, or Daily Output quantities.
    /// </summary>
    public class WorkerCutReport
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string StoreInRecordId { get; set; } = string.Empty;
        public string ProductionRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string InAdNo { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string JobNo { get; set; } = string.Empty;
        public string CutInDate { get; set; } = string.Empty;

        public int InQty { get; set; }
        public int TotalCutQty { get; set; }
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }
        public int BundleCount { get; set; }

        /// <summary>
        /// Process date selected by the Worker page. Stored as yyyy-MM-dd for safe filtering.
        /// </summary>
        public string ReportDate { get; set; } = string.Empty;

        public string WorkerName { get; set; } = string.Empty;

        /// <summary>
        /// Serialized List&lt;WorkerCutReportRowDto&gt;.
        /// Kept as a plain string to avoid converter/value-comparer changes.
        /// </summary>
        public string RowsJson { get; set; } = "[]";

        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
