using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
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
        
        // EF Core 8/9 can map this directly to a JSON array in SQL Server
        public List<string> Placements { get; set; } = new();
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
    }
}