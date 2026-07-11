using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// Saved snapshot of a Reconciliation Report.
    /// This is read/reporting data only. It does not affect Store-In, Gatepass, CPI, stock, or Advice Note logic.
    /// RowsJson stores the exact received/sent table rows that were visible when the user clicked Save.
    /// </summary>
    public class ReconciliationReportRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string JobNos { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string PoNo { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;

        /// <summary>
        /// Date the report was saved. Stored as yyyy-MM-dd for safe frontend filtering.
        /// </summary>
        public string ReportDate { get; set; } = string.Empty;

        public int ReceivedQty { get; set; }
        public int SentTotal { get; set; }
        public int PdTotal { get; set; }
        public int FdTotal { get; set; }
        public int SampleTestingTotal { get; set; }
        public int RtnTotal { get; set; }
        public int GoodQtyTotal { get; set; }

        /// <summary>
        /// Serialized List&lt;ReconciliationSavedRowDto&gt;.
        /// Kept as plain string to avoid EF JSON converter/value-comparer changes.
        /// </summary>
        public string RowsJson { get; set; } = "[]";

        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}