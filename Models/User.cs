using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; 
    }
}