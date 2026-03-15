using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class StoreInRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string SubmissionId { get; set; } = string.Empty;

        public int RevisionNo { get; set; } = 1;

        public string? StyleNo { get; set; }
        public string? CustomerName { get; set; }

        [Required]
        public string ScheduleNo { get; set; } = string.Empty;

        [Required]
        public string CutNo { get; set; } = string.Empty;

        public string? BodyColour { get; set; }
        public string? PrintColour { get; set; }
        public string? Components { get; set; }
        public string? Season { get; set; }
        public string? CutInDate { get; set; }

        public int BulkQty { get; set; }
        public int InQty { get; set; }
        public int BalanceBulkQty { get; set; }
        public int CutQty { get; set; }
        public int AvailableQty { get; set; }

        public int BundleQty { get; set; }
        public string? NumberRange { get; set; }
        public string? Size { get; set; }
    }

    public class StoreProductionRecord
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string StoreInRecordId { get; set; } = string.Empty;

    public string? SubmissionId { get; set; }

    public int RevisionNo { get; set; } = 1;

    public string? IssueDate { get; set; }
    public string? StyleNo { get; set; }
    public string? CustomerName { get; set; }
    public string? Components { get; set; }
    public string? CutNo { get; set; }

    public int IssueQty { get; set; }
    public int BalanceQty { get; set; }

    public string? LineNo { get; set; }
}
}