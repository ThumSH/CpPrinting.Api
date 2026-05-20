// Models/SampleStyle.cs
namespace CpPrinting.Api.Models
{
    // ==========================================
    // SAMPLE STYLE REVISION
    // Each time the client gives feedback, the
    // developer adds a comment. System auto-numbers
    // it as Revision 1, 2, 3...
    // Stored as JSON column on SampleStyle.
    // ==========================================
    public class SampleStyleRevision
    {
        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public int    RevisionNo  { get; set; }
        public string Comment     { get; set; } = string.Empty;
        public string CreatedAt   { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        public string CreatedBy   { get; set; } = string.Empty;
    }

    // ==========================================
    // SAMPLE STYLE
    // One record per component per style.
    // e.g. AD001-Front-Black and AD001-Back-White
    // are two separate rows under the same DevelopmentJob.
    // ==========================================
    public class SampleStyle
    {
        public string Id { get; set; } = string.Empty;

        // ── Linked job info ───────────────────────────────────────────────────
        public string DevelopmentJobId  { get; set; } = string.Empty;
        public string Customer          { get; set; } = string.Empty;
        public string StyleNo           { get; set; } = string.Empty;
        public string Season            { get; set; } = string.Empty;
        public string PrintingTechnique { get; set; } = string.Empty;
        public string BodyColour        { get; set; } = string.Empty;
        public string PrintColour       { get; set; } = string.Empty;
        public string PrintColourQty    { get; set; } = string.Empty;
        public string WashingStandard   { get; set; } = string.Empty;

        /// <summary>e.g. "Front", "Back", "Sleeve", "Pocket"</summary>
        public string Component { get; set; } = string.Empty;

        // ── Artwork image ─────────────────────────────────────────────────────
        public string? ImagePath { get; set; }

        // ── Client revision history (JSON column) ─────────────────────────────
        // Each entry = one client feedback comment added by the developer.
        // Auto-numbered Revision 1, 2, 3...
        // Register in AppDbContext.OnModelCreating() — see bottom of this file.
        public List<SampleStyleRevision> Revisions { get; set; } = new();

        // ── Developer workflow ────────────────────────────────────────────────
        public bool    ClientApproved   { get; set; } = false;
        public string? ClientApprovedAt { get; set; }
        public string? ClientApprovedBy { get; set; }

        // ── Admin approval ────────────────────────────────────────────────────
        public string  AdminStatus   { get; set; } = "Pending";
        public string? AdminRemarks  { get; set; }
        public string? AdminActionAt { get; set; }
        public string? AdminActionBy { get; set; }

        // ── Submission details ────────────────────────────────────────────────
        public string? RcMeetingDate     { get; set; }
        public string? AcNumber          { get; set; }
        public string? BoardSet          { get; set; }
        public string? BulkQty           { get; set; }
        public string? DeveloperComments { get; set; }

        public bool    SubmittedToAdmin { get; set; } = false;
        public string? SubmittedAt      { get; set; }

        // ── Timestamps ────────────────────────────────────────────────────────
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
