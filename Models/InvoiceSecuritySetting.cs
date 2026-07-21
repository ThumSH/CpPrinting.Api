using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CpPrinting.Api.Models
{
    public class InvoiceSecuritySetting
    {
        [Key]
        public string Id { get; set; } = "invoice-security";

        // The real invoice alteration password is never stored.
        // Only its BCrypt hash is saved.
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
    }
}