using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class CPIRowData
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string Part { get; set; } = string.Empty;
        public string SampleSize { get; set; } = string.Empty;
        public string DefectedBefore { get; set; } = string.Empty;
        public string DefectedAfter { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class CPIReport
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; } = 1;

        public string Date { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;

        public int ReceivedQty { get; set; }
        public int CpiQty { get; set; }

        public Dictionary<string, CPIRowData> InspectionRows { get; set; } = new();

        public int CuttingQty { get; set; }
        public int CheckedQty { get; set; }
        public int RejDamageQty { get; set; }
        public string RejectionPercentage { get; set; } = string.Empty;
        public int BalanceQty { get; set; }

        // Final QC gate for Production
        public string InspectionStatus { get; set; } = "Pending"; // Pending, Passed, Failed

        public string AppRej { get; set; } = string.Empty;
        public string CheckedBy { get; set; } = string.Empty;
        public string SummaryDate { get; set; } = string.Empty;
    }
}