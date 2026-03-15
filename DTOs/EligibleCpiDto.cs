namespace CpPrinting.Api.DTOs
{
    public class EligibleCpiDto
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; }

        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string Components { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;

        public int ReceivedQty { get; set; }
        public string CutInDate { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public int BundleQty { get; set; }
        public string NumberRange { get; set; } = string.Empty;
    }
}