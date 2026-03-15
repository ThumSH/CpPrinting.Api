namespace CpPrinting.Api.DTOs
{
    public class EligibleAuditDto
    {
        public string DeliveryTrackerReportId { get; set; } = string.Empty;
        public string AdviceNoteId { get; set; } = string.Empty;
        public string ProductionRecordId { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string AdNo { get; set; } = string.Empty;
        public string DeliveryStatus { get; set; } = string.Empty;

        public int DeliveryQty { get; set; }
        public int RemainingAuditQty { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}