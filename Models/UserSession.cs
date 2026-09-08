using System;
using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class UserSession
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        public string LoginAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        public string LogoutAt { get; set; } = string.Empty;
        public string LastSeenAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        public int TotalLoginSeconds { get; set; }
        public int TotalActiveSeconds { get; set; }
        public int TotalIdleSeconds { get; set; }

        // User, SystemIdle, TokenExpired, BrowserClosed, Forced, NewLogin
        public string LogoutType { get; set; } = string.Empty;
        public string LogoutReason { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}
