// Models/SampleStyle.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace CpPrinting.Api.Models
{
    // ── Revision entry ────────────────────────────────────────────────────────
    // DEPLOYMENT NOTE:
    //   PreviousArtworkUrl lives inside the Revisions JSON column — no DB migration needed.
    //   ArtworkUrl semantics changed: null now means "no new artwork this revision"
    //   (previously it was always set to either the new URL or the existing ImagePath).
    //   Old revision records in the DB still have ArtworkUrl set — they continue to render.
    //   New revisions without a new upload will have ArtworkUrl = null and show nothing.
    //   This is the correct, intended behavior.
    public class SampleStyleRevision
    {
        public string  Id              { get; set; } = Guid.NewGuid().ToString();
        public int     RevisionNo      { get; set; }
        public string  Comment         { get; set; } = string.Empty;

        /// <summary>
        /// The artwork active BEFORE this revision. Stored in the Revisions JSON
        /// column — zero DB migration required.
        /// </summary>
        public string? PreviousArtworkUrl { get; set; }

        /// <summary>
        /// New artwork uploaded WITH this revision. Null when no artwork changed.
        /// When non-null, style.ImagePath is updated to this value.
        /// </summary>
        public string? ArtworkUrl { get; set; }

        public string  CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        public string  CreatedBy { get; set; } = string.Empty;
    }

    // ── Sample Style ──────────────────────────────────────────────────────────
    public class SampleStyle
    {
        public string Id { get; set; } = string.Empty;

        public string DevelopmentJobId  { get; set; } = string.Empty;
        public string Customer          { get; set; } = string.Empty;
        public string StyleNo           { get; set; } = string.Empty;
        public string Season            { get; set; } = string.Empty;
        public string PrintingTechnique { get; set; } = string.Empty;

        public string BodyColour      { get; set; } = string.Empty;
        public string PrintColour     { get; set; } = string.Empty;
        public string PrintColourQty  { get; set; } = string.Empty;
        public string WashingStandard { get; set; } = string.Empty;

        public string Component { get; set; } = string.Empty;

        // ── Artwork ───────────────────────────────────────────────────────────

        /// <summary>
        /// [NotMapped] — NOT stored in DB, no migration required.
        /// Computed by the controller on each response: derived from the first
        /// revision's PreviousArtworkUrl when revisions exist, otherwise equals
        /// ImagePath. Sent in JSON so the frontend can show "original vs current".
        /// </summary>
        [NotMapped]
        public string? OriginalImagePath { get; set; }

        /// <summary>
        /// The current (latest) artwork URL. Persisted to DB. Updated when a
        /// revision uploads a new artwork.
        /// </summary>
        public string? ImagePath { get; set; }

        // ── Revision history (stored as JSON column) ──────────────────────────
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

        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}