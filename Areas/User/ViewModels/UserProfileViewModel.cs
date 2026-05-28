using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.User.ViewModels
{
    public class UserProfileViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        public string Role { get; set; } = "User";
        public DateTime CreatedAt { get; set; }

        public string? AvatarUrl { get; set; }

        [Display(Name = "Mật khẩu hiện tại")]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Mật khẩu mới")]
        public string? NewPassword { get; set; }

        [Display(Name = "Xác nhận mật khẩu mới")]
        public string? ConfirmNewPassword { get; set; }

        public List<UserReviewHistoryItemViewModel> ReviewHistory { get; set; } = new();
    }

    public class UserReviewHistoryItemViewModel
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime ActivityAt { get; set; }
        public bool IsEdited { get; set; }
    }
}

