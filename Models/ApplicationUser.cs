using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WaitWise.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Role { get; set; }

        public int? AssignedCounterId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
