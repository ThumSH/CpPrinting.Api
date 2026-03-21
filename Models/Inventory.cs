using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// Parent record: one Store-In entry per schedule received against a style.
    /// IN Qty deducts from the global Bulk Qty for the style.
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

        [Required]
        public string ScheduleNo { get; set; } = string.Empty;

        public string? CutInDate { get; set; }

        /// <summary>
        /// The approved bulk qty snapshot at time of entry (from ApprovalRecord).
        /// </summary>
        public int BulkQty { get; set; }

        /// <summary>
        /// Total qty received in THIS store-in entry. Deducts from global bulk balance.
        /// </summary>
        public int InQty { get; set; }

        /// <summary>
        /// Computed by backend: BulkQty - SUM(InQty) across ALL StoreInRecords for this SubmissionId.
        /// Stored as a denormalized snapshot for display convenience.
        /// </summary>
        public int BalanceBulkQty { get; set; }

        /// <summary>
        /// Sum of all CutRecord.CutQty under this entry. Should equal InQty when fully cut.
        /// </summary>
        public int TotalCutQty { get; set; }

        /// <summary>
        /// InQty - TotalCutQty = qty received but not yet assigned to cuts.
        /// </summary>
        public int UncutBalance { get; set; }

        /// <summary>
        /// InQty minus qty already issued to production. This is the shelf stock.
        /// </summary>
        public int AvailableQty { get; set; }

        // Navigation property — EF Core will load these
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
        /// Qty assigned to this cut. Sum of all cuts under a StoreIn must not exceed InQty.
        /// </summary>
        public int CutQty { get; set; }

        // Navigation property
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

        public int BundleQty { get; set; }

        public string Size { get; set; } = string.Empty;

        public string? NumberRange { get; set; }
    }

    /// <summary>
    /// Production issue record — unchanged structure, but now references the parent StoreInRecord.
    /// The CutNo field tells which cut this production issue is against.
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