using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CpPrinting.Api.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        
        // This holds the scrambled password.
        // [JsonIgnore] prevents the API from ever accidentally sending the hash to the frontend.
        [JsonIgnore] 
        public string PasswordHash { get; set; } = string.Empty; 
        
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; 
    }
}