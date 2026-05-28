namespace tramsac99.Areas.User.ViewModels
{
    public class SupportTicketItemViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? AdminReply { get; set; }
        public DateTime? LastStatusChangedAt { get; set; }
        public bool IsUserSeen { get; set; }
        public DateTime? UserSeenAt { get; set; }

        // Why changed: lightweight AI classification for support tickets.
        public string Category { get; set; } = "Khác";

        // Why changed: help admin and user quickly see urgency.
        public string Priority { get; set; } = "Trung bình";

        // Why changed: show a short summary line instead of making users read the full ticket first.
        public string AiSummary { get; set; } = string.Empty;
    }
}
