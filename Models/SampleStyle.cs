// Models/SampleStyle.cs
namespace CpPrinting.Api.Models
{
    public class SampleStyle
    {
        public string Id { get; set; } = string.Empty;

        // ── Linked job info ───────────────────────────────────────────────────
        public string DevelopmentJobId { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string PrintingTechnique { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string PrintColourQty { get; set; } = string.Empty;
        public string WashingStandard { get; set; } = string.Empty;
        // Single component this sample covers: Front, Back, Sleeve, Pocket, Waistband, Other.
        // Replaces the old Placements field.
        public string Component { get; set; } = string.Empty;

        // ── Artwork image ─────────────────────────────────────────────────────
        public string? ImagePath { get; set; }

        // ── Developer workflow ────────────────────────────────────────────────
        public bool ClientApproved { get; set; } = false;
        public string? ClientApprovedAt { get; set; }
        public string? ClientApprovedBy { get; set; }

        // ── Admin approval ────────────────────────────────────────────────────
        public string AdminStatus { get; set; } = "Pending";
        public string? AdminRemarks { get; set; }
        public string? AdminActionAt { get; set; }
        public string? AdminActionBy { get; set; }

        // ── Submission details ────────────────────────────────────────────────
        public string? RcMeetingDate { get; set; }
        public string? AcNumber { get; set; }
        public string? BoardSet { get; set; }
        public string? BulkQty { get; set; }

        /// <summary>
        /// Developer comments written at submission time — visible to admin.
        /// </summary>
        public string? DeveloperComments { get; set; }

        public bool SubmittedToAdmin { get; set; } = false;
        public string? SubmittedAt { get; set; }

        // ── Timestamps ────────────────────────────────────────────────────────
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}