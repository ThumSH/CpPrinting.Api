using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// Parent record: one Store-In entry per schedule received against a style/component.
    /// IN Qty deducts from the global Bulk Qty for the component submission.
    /// </summary>
    public class StoreInRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string SubmissionId { get; set; } = string.Empty;

        public int RevisionNo { get; set; } = 1;

        public string? StyleNo { get; set; }
        public string? CustomerName { get; set; }
        public string? BodyColour { get; set; }
        public string? PrintColour { get; set; }
        public string? Components { get; set; }
        public string? Season { get; set; }

        /// <summary>
        /// IN-AD number entered during Store-In.
        /// This is used only for Delivery Tracker display/reporting.
        /// It does not affect stock balance, QC, production, or gatepass logic.
        /// </summary>
        public string? InAdNo { get; set; }

        /// <summary>
        /// Optional schedule number. Some styles may not use schedules.
        /// </summary>
        public string ScheduleNo { get; set; } = string.Empty;

        public string? CutInDate { get; set; }

        /// <summary>
        /// The approved bulk qty snapshot at time of entry.
        /// </summary>
        public int BulkQty { get; set; }

        /// <summary>
        /// Total qty received in this Store-In entry.
        /// </summary>
        public int InQty { get; set; }

        /// <summary>
        /// Computed by backend: BulkQty - SUM(InQty) across all StoreInRecords for this SubmissionId.
        /// Stored as a denormalized snapshot for display convenience.
        /// </summary>
        public int BalanceBulkQty { get; set; }

        /// <summary>
        /// Sum of all CutRecord.CutQty under this Store-In entry.
        /// </summary>
        public int TotalCutQty { get; set; }

        /// <summary>
        /// InQty - TotalCutQty.
        /// </summary>
        public int UncutBalance { get; set; }

        /// <summary>
        /// InQty minus qty already issued to production.
        /// </summary>
        public int AvailableQty { get; set; }

        public List<CutRecord> Cuts { get; set; } = new();
    }

    /// <summary>
    /// Child of StoreInRecord. Each cut within a store-in entry.
    /// </summary>
    public class CutRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string StoreInRecordId { get; set; } = string.Empty;

        [ForeignKey(nameof(StoreInRecordId))]
        public StoreInRecord? StoreInRecord { get; set; }

        [Required]
        public string CutNo { get; set; } = string.Empty;

        /// <summary>
        /// Which component submission this cut belongs to.
        /// </summary>
        public string SubmissionId { get; set; } = string.Empty;

        public int CutQty { get; set; }

        public List<BundleRecord> Bundles { get; set; } = new();
    }

    /// <summary>
    /// Child of CutRecord. Each bundle within a cut.
    /// </summary>
    public class BundleRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string CutRecordId { get; set; } = string.Empty;

        [ForeignKey(nameof(CutRecordId))]
        public CutRecord? CutRecord { get; set; }

        [Required]
        public string BundleNo { get; set; } = string.Empty;

        /// <summary>
        /// Preserves the exact manual row order entered by Stores.
        /// Do not sort by BundleNo because b-10/b-13/b-9 must stay in entered order.
        /// </summary>
        public int BundleOrder { get; set; }

        public int BundleQty { get; set; }

        public string Size { get; set; } = string.Empty;

        public string? NumberRange { get; set; }
    }

    /// <summary>
    /// Production issue record.
    /// </summary>
    public class StoreProductionRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string StoreInRecordId { get; set; } = string.Empty;

        public string? SubmissionId { get; set; }

        public int RevisionNo { get; set; } = 1;

        public string? IssueDate { get; set; }
        public string? StyleNo { get; set; }
        public string? CustomerName { get; set; }
        public string? Components { get; set; }
        public string? CutNo { get; set; }

        public int IssueQty { get; set; }
        public int BalanceQty { get; set; }

        public string? LineNo { get; set; }
    }
}