using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.Admin.Models
{
    public class PasswordResetToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public AppUser? User { get; set; }

        [Required]
        [StringLength(200)]
        public string Token { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }

        [StringLength(50)]
        public string? RequestedByIp { get; set; }
    }
}

