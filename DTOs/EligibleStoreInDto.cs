namespace CpPrinting.Api.DTOs
{
    // ==========================================
    // ELIGIBLE STYLES for Store-In
    // (same as before — from approved submissions)
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
        public string Components { get; set; } = string.Empty;
        public int ApprovedBulkQty { get; set; }

        /// <summary>
        /// Remaining bulk qty available for new store-in entries.
        /// Calculated: ApprovedBulkQty - SUM(InQty) across all existing StoreInRecords.
        /// </summary>
        public int RemainingBulkQty { get; set; }
    }

    // ==========================================
    // CREATE REQUEST — sent from frontend
    // Single form with nested cuts and bundles
    // ==========================================
    public class CreateStoreInRequest
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string? CutInDate { get; set; }
        public int InQty { get; set; }
        public List<CreateCutRequest> Cuts { get; set; } = new();
    }

    public class CreateCutRequest
    {
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }
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
    // RESPONSE — returned to frontend with full hierarchy
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
        public List<CutResponseDto> Cuts { get; set; } = new();
    }

    public class CutResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }
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
    // BULK BALANCE — global per-style summary
    // ==========================================
    public class BulkBalanceDto
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int ApprovedBulkQty { get; set; }
        public int TotalInQty { get; set; }
        public int RemainingBulkQty { get; set; }
        public int EntryCount { get; set; }
    }
}