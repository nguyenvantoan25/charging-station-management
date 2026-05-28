using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.Admin.Models
{
    public class SupportRequest
    {
        public int Id { get; set; }

        public int? SenderUserId { get; set; } // Why changed: keep a link to logged in user when available.

        [StringLength(100)]
        public string? SenderUserName { get; set; } // Why changed: show username quickly on admin support page.

        [Required]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Mới";

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReadAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [StringLength(1000)]
        public string? AdminReply { get; set; } // Why changed: let admin leave a business-style reply when closing a ticket.

        public DateTime? LastStatusChangedAt { get; set; } // Why changed: support badge on user side needs a stable timestamp.

        public bool IsUserSeen { get; set; } = true; // Why changed: user sees new notification only after admin changes status.

        public DateTime? UserSeenAt { get; set; }
    }
}
