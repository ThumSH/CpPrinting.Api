namespace CpPrinting.Api.DTOs
{
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
    }
}