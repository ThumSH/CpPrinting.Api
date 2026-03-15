using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class ApprovalRecord
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string SubmissionId { get; set; } = string.Empty;

        public string StyleNo { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string Level { get; set; } = string.Empty;

        public int RevisionNo { get; set; } = 1;

        public string Status { get; set; } = string.Empty; // Pending, Approved, Rejected

        public string? BoardSet { get; set; }

        public string? ApprovalCard { get; set; }

        public string? RaMeetingDate { get; set; }

        public string? BulkOrderQty { get; set; }

        public string ReviewedAt { get; set; } = string.Empty;
    }
}