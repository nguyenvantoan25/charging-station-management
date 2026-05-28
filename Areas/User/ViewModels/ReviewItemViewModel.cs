namespace tramsac99.Areas.User.ViewModels
{
    public class ReviewItemViewModel
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string? UserName { get; set; }
        public DateTime CreatedAt { get; set; }

        // Why changed: show the edited time on user/admin views.
        public DateTime? UpdatedAt { get; set; }

        // Why changed: easy flag for UI to display "đã chỉnh sửa".
        public bool IsEdited => UpdatedAt.HasValue && UpdatedAt.Value > CreatedAt;
    }
}