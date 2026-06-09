namespace CpPrinting.Api.DTOs
{
    public class GatepassBundleDto
    {
        public string BundleNo { get; set; } = string.Empty;

        /// <summary>
        /// Manual row order saved from Store-In. Do not use BundleNo sorting.
        /// </summary>
        public int BundleOrder { get; set; }

        public int BundleQty { get; set; }
        public string Size { get; set; } = string.Empty;
        public string NumberRange { get; set; } = string.Empty;
    }

    public class GatepassCutDto
    {
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }

        /// <summary>
        /// The component (Part) locked in by the CPI inspection for this specific cut.
        /// Flows from CPI → Production → Gatepass.
        /// </summary>
        public string Part { get; set; } = string.Empty;

        public List<GatepassBundleDto> Bundles { get; set; } = new();
    }

    public class EligibleGatepassDto
    {
        public string ProductionRecordId { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string IssueDate { get; set; } = string.Empty;
        public string LineNo { get; set; } = string.Empty;

        public int IssueQty { get; set; }
        public int RemainingDispatchQty { get; set; }

        // Enriched from Store-In record
        public string ScheduleNo { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;

        // Cuts and bundles from Store-In (for the gatepass bundle table)
        public List<GatepassCutDto> Cuts { get; set; } = new();
    }
}