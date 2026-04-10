namespace CpPrinting.Api.DTOs
{
    public class ProductionCutDto
    {
        public string CutRecordId { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }

        /// <summary>
        /// The component (Part) locked in by the CPI inspection for this specific cut.
        /// This flows downstream to Production → Worker → Gatepass → Audit.
        /// </summary>
        public string Part { get; set; } = string.Empty;

        /// <summary>
        /// How much of this cut's qty has already been issued to production.
        /// </summary>
        public int AlreadyIssued { get; set; }

        /// <summary>
        /// CutQty - AlreadyIssued = remaining available for this cut.
        /// </summary>
        public int AvailableQty { get; set; }
    }

    public class EligibleProductionDto
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;

        // Global bulk balance for the style
        public int BulkQty { get; set; }
        public int BulkBalance { get; set; }

        // Total available across all cuts
        public int TotalAvailableQty { get; set; }

        public string InspectionStatus { get; set; } = string.Empty;
        public string CheckedBy { get; set; } = string.Empty;
        public string SummaryDate { get; set; } = string.Empty;

        // Per-cut breakdown (now includes Part from CPI)
        public List<ProductionCutDto> Cuts { get; set; } = new();
    }
}