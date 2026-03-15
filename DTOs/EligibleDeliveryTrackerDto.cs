namespace CpPrinting.Api.DTOs
{
    public class EligibleDeliveryTrackerDto
    {
        public string AdviceNoteId { get; set; } = string.Empty;
        public string ProductionRecordId { get; set; } = string.Empty;
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string AdNo { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;

        public int DispatchQty { get; set; }
        public int RemainingTrackableQty { get; set; }
        public string DeliveryDate { get; set; } = string.Empty;
    }
}