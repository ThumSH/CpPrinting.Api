using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    public class Operator
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;  // Which role account they belong to
        public bool IsActive { get; set; } = true;
    }
}