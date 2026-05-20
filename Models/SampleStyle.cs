// Models/SampleStyle.cs
namespace CpPrinting.Api.Models
{
    // ── Revision entry ────────────────────────────────────────────────────────
    // Each client feedback = one revision. Dev adds comment + optionally
    // replaces the artwork. Auto-numbered 1, 2, 3...
    public class SampleStyleRevision
    {
        public string  Id         { get; set; } = Guid.NewGuid().ToString();
        public int     RevisionNo { get; set; }
        public string  Comment    { get; set; } = string.Empty;
        // Artwork at time of this revision (may differ from the original ImagePath)
        public string? ArtworkUrl { get; set; }
        public string  CreatedAt  { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        public string  CreatedBy  { get; set; } = string.Empty;
    }

    // ── Sample Style ──────────────────────────────────────────────────────────
    // One record per component per style.
    // AD001-Front and AD001-Back are two separate rows.
    // A style+component can have multiple body colours — stored as comma-separated.
    public class SampleStyle
    {
        public string Id { get; set; } = string.Empty;

        // ── Linked job ────────────────────────────────────────────────────────
        public string DevelopmentJobId  { get; set; } = string.Empty;
        public string Customer          { get; set; } = string.Empty;
        public string StyleNo           { get; set; } = string.Empty;
        public string Season            { get; set; } = string.Empty;
        public string PrintingTechnique { get; set; } = string.Empty;

        public string BodyColour     { get; set; } = string.Empty;
        public string PrintColour    { get; set; } = string.Empty;
        public string PrintColourQty { get; set; } = string.Empty;
        public string WashingStandard { get; set; } = string.Empty;

        /// <summary>"Front" | "Back" | "Sleeve" | "Pocket" | "Waistband" | "Other"</summary>
        public string Component { get; set; } = string.Empty;

        // ── Current artwork ───────────────────────────────────────────────────
        // Always reflects the latest revision artwork (or the original if no revision).
        public string? ImagePath { get; set; }

        // ── Revision history (JSON column) ────────────────────────────────────
        // Each entry captures the client comment + artwork at that revision.
        // Register in AppDbContext.OnModelCreating() — see bottom of file.
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

        // ── Timestamps ───────────────────────────────────────────────────────
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}

// ==========================================
// AppDbContext.OnModelCreating() — ADD THIS BLOCK
// (after the existing DOWNTIME block, before closing brace)
//
//   modelBuilder.Entity<SampleStyle>()
//       .Property(e => e.Revisions)
//       .HasConversion(
//           v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
//           v => string.IsNullOrWhiteSpace(v)
//               ? new List<SampleStyleRevision>()
//               : JsonSerializer.Deserialize<List<SampleStyleRevision>>(v,
//                    (JsonSerializerOptions?)null) ?? new()
//       );
//
// Then run:
//   dotnet ef migrations add AddRevisionArtwork
//   dotnet ef database update --connection "Server=192.168.1.100;..."
// ==========================================