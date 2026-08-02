using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class Customer
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TinNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TelephoneNumber { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } =
            DateTime.UtcNow;

        public string UpdatedBy { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }
    }
}