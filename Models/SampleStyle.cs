// Models/SampleStyle.cs
namespace CpPrinting.Api.Models
{
    public class SampleStyle
    {
        public string Id { get; set; } = string.Empty;

        // ── Linked job info (copied from DevelopmentJob at creation) ──────────
        public string DevelopmentJobId { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string PrintingTechnique { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string PrintColourQty { get; set; } = string.Empty;
        public string WashingStandard { get; set; } = string.Empty;
        public string Placements { get; set; } = string.Empty; // comma-separated

        // ── Artwork image ─────────────────────────────────────────────────────
        // Stored as a relative path, e.g. "uploads/samples/abc123.png"
        // Served by the backend as a static file.
        public string? ImagePath { get; set; }

        // ── Developer workflow ────────────────────────────────────────────────
        // Step 1: Created automatically when DevelopmentJob is created
        // Step 2: Developer marks client approved
        public bool ClientApproved { get; set; } = false;
        public string? ClientApprovedAt { get; set; }
        public string? ClientApprovedBy { get; set; }

        // ── Admin approval ────────────────────────────────────────────────────
        // "Pending" | "Approved" | "Rejected"
        public string AdminStatus { get; set; } = "Pending";
        public string? AdminRemarks { get; set; }
        public string? AdminActionAt { get; set; }
        public string? AdminActionBy { get; set; }

        // ── Submission details (filled by Developer before submitting to Admin) 
        public string? RcMeetingDate { get; set; }
        public string? AcNumber { get; set; }
        public string? BoardSet { get; set; }
        public string? BulkQty { get; set; }
        public bool SubmittedToAdmin { get; set; } = false;
        public string? SubmittedAt { get; set; }

        // ── Timestamps ────────────────────────────────────────────────────────
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}