namespace CpPrinting.Api.DTOs
{
    public class CpiBundleDto
    {
        public string BundleNo { get; set; } = string.Empty;
        public int BundleQty { get; set; }
        public string Size { get; set; } = string.Empty;
        public string NumberRange { get; set; } = string.Empty;
    }

    public class CpiCutDto
    {
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }
        public List<CpiBundleDto> Bundles { get; set; } = new();
    }

    public class EligibleCpiDto
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;

        public int ReceivedQty { get; set; }
        public string CutInDate { get; set; } = string.Empty;

        // Aggregated summary fields
        public int CutCount { get; set; }
        public int TotalCutQty { get; set; }
        public int TotalBundleCount { get; set; }

        // First cut/bundle for backward compat display
        public string CutNo { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public int BundleQty { get; set; }
        public string NumberRange { get; set; } = string.Empty;

        // Full hierarchy for the CPI inspection grid
        public List<CpiCutDto> Cuts { get; set; } = new();
    }
}