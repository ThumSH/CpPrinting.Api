using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// One DevelopmentJob = one style + one component + one body colour + one bulk qty.
    /// For a style with Front and Back, create two separate DevelopmentJobs.
    /// </summary>
    public class DevelopmentJob
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Customer { get; set; } = string.Empty;

        [Required]
        public string StyleNo { get; set; } = string.Empty;

        public string Season { get; set; } = string.Empty;
        public string PrintingTechnique { get; set; } = string.Empty;
        public string ArtworkFileName { get; set; } = string.Empty;
        public string ArtworkPreviewUrl { get; set; } = string.Empty;
        public string WashingStandard { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public string PrintColourQty { get; set; } = string.Empty;
        public string SampleOrderedDate { get; set; } = string.Empty;
        public string SampleDeliveryDate { get; set; } = string.Empty;

        /// <summary>
        /// Single component this job covers: Front, Back, Sleeve, Pocket, Waistband, Other.
        /// Replaces the old Placements list — one job, one component.
        /// </summary>
        public string Component { get; set; } = string.Empty;
    }

    public class SubmissionForm
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string SubmissionDate { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public int RevisionNo { get; set; } = 1;
        public bool IsLatestRevision { get; set; } = true;
    }
}