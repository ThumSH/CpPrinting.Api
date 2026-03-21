using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class AuditBundleSelection
    {
        public string BundleNo { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public int Qty { get; set; }
    }

    public class AuditRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; } = 1;

        public string Date { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public string Sizes { get; set; } = string.Empty;

        public List<AuditBundleSelection> Bundles { get; set; } = new();

        public int ReleaseQty { get; set; }
        public int AuditQty { get; set; }
        public string Status { get; set; } = "Pending";

        public string AuditorName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}