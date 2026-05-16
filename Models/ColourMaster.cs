using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// Master list of body colours. Admin-managed.
    /// Developers pick from this list when creating a job — prevents free-text duplicates.
    /// Existing downstream fields (StoreIn, CPI, etc.) remain plain strings — no migration needed.
    /// </summary>
    public class ColourMaster
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// e.g. "R-1 — Bright Red", "N-4 — Navy Blue"
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional hex code for visual swatch in the UI, e.g. "#FF4444"
        /// </summary>
        public string? HexCode { get; set; }

        /// <summary>
        /// Soft-delete: inactive colours won't appear in dropdowns
        /// but existing job records still reference them by value string.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Sort order for the dropdown (lower = higher in list).
        /// Defaults to 0 — ties sorted alphabetically.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
    }
}