namespace CpPrinting.Api.DTOs
{
    public class EligibleProductionDto
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;

        public int AvailableQty { get; set; }
        public string InspectionStatus { get; set; } = string.Empty;
        public string CheckedBy { get; set; } = string.Empty;
        public string SummaryDate { get; set; } = string.Empty;
    }
}