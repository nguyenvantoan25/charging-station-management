using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.Admin.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        public string? FullName { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty; // Why changed: keep existing login flow stable.

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "User";

        public bool IsBlocked { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? AvatarUrl { get; set; } // Why changed: store uploaded avatar path for profile and admin page.
    }
}
