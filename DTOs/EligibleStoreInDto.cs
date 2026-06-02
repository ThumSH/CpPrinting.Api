namespace CpPrinting.Api.DTOs
{
    // ==========================================
    // ELIGIBLE STYLES for Store-In
    // One row per approved component-submission.
    // Frontend groups by StyleNo + CustomerName.
    // ==========================================
    public class EligibleStoreInDto
    {
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string SubmissionDate { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string ReviewedAt { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;

        /// <summary>
        /// Single component string: "Front", "Back", "Sleeve" etc.
        /// Replaces old comma-joined Components list.
        /// </summary>
        public string Components { get; set; } = string.Empty;

        public int ApprovedBulkQty { get; set; }

        /// <summary>
        /// Remaining bulk for this specific component-submission.
        /// Calculated: ApprovedBulkQty - SUM(CutQty) for cuts with this SubmissionId.
        /// </summary>
        public int RemainingBulkQty { get; set; }
    }

    // ==========================================
    // CREATE REQUEST — sent from frontend
    // ==========================================
    public class CreateStoreInRequest
    {
        public string SubmissionId { get; set; } = string.Empty;  // primary submission (first component)
        public string ScheduleNo { get; set; } = string.Empty;

        public string InAdNo { get; set; } = string.Empty;
        public string? CutInDate { get; set; }
        public int InQty { get; set; }
        public List<CreateCutRequest> Cuts { get; set; } = new();
    }

    public class CreateCutRequest
    {
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }

        /// <summary>
        /// Which component-submission this cut belongs to.
        /// Required for per-component bulk balance tracking.
        /// e.g. the Front cut carries the Front submission's Id,
        ///      the Back cut carries the Back submission's Id.
        /// </summary>
        public string SubmissionId { get; set; } = string.Empty;

        public List<CreateBundleRequest> Bundles { get; set; } = new();
    }

    public class CreateBundleRequest
    {
        public string BundleNo { get; set; } = string.Empty;
        public int BundleQty { get; set; }
        public string Size { get; set; } = string.Empty;
        public string? NumberRange { get; set; }
    }

    // ==========================================
    // RESPONSE — returned to frontend
    // ==========================================
    public class StoreInResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutInDate { get; set; } = string.Empty;
        public int BulkQty { get; set; }
        public int InQty { get; set; }
        public int BalanceBulkQty { get; set; }
        public int TotalCutQty { get; set; }
        public int UncutBalance { get; set; }
        public int AvailableQty { get; set; }

        public string InAdNo { get; set; } = string.Empty;
        public List<CutResponseDto> Cuts { get; set; } = new();
    }

    public class CutResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }

        /// <summary>
        /// Which component-submission this cut belongs to.
        /// Returned to frontend so it knows which component each cut covers.
        /// </summary>
        public string SubmissionId { get; set; } = string.Empty;

        public List<BundleResponseDto> Bundles { get; set; } = new();
    }

    public class BundleResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string BundleNo { get; set; } = string.Empty;
        public int BundleQty { get; set; }
        public string Size { get; set; } = string.Empty;
        public string NumberRange { get; set; } = string.Empty;
    }

    // ==========================================
    // BULK BALANCE — per component-submission
    // ==========================================
    public class BulkBalanceDto
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Which component this balance row represents: "Front", "Back" etc.
        /// </summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// Body colour for this component.
        /// </summary>
        public string BodyColour { get; set; } = string.Empty;

        public int ApprovedBulkQty { get; set; }
        public int TotalInQty { get; set; }
        public int RemainingBulkQty { get; set; }
        public int EntryCount { get; set; }
    }
}