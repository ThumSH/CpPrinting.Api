using System;
using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class UserSessionEvent
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        // Login, Heartbeat, IdleWarningShown, UserActiveAgain, UserLogout, SystemIdleLogout, SessionExpired
        public string EventType { get; set; } = string.Empty;
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        public int IdleSeconds { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
