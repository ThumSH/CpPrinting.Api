namespace CpPrinting.Api.DTOs
{
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
    }
}