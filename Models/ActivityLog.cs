using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class ActivityLog
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        // Who
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        // What
        public string Action { get; set; } = string.Empty;       // Login, Create, Update, Delete, Print, Export
        public string Entity { get; set; } = string.Empty;       // StoreIn, CPI, Production, AdviceNote, Audit, DailyOutput, Downtime, Approval, User
        public string EntityId { get; set; } = string.Empty;     // The ID of the record affected
        public string Description { get; set; } = string.Empty;  // Human-readable: "Created store-in for NK-2004 (500 pcs)"

        // When
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        // Where (optional)
        public string IpAddress { get; set; } = string.Empty;
    }
}