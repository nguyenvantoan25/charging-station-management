using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace tramsac99.Areas.Admin.ViewModels
{
    public class UserEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Vai trò không được để trống.")]
        [Display(Name = "Vai trò")]
        public string Role { get; set; } = "User";

        [Display(Name = "Khóa tài khoản")]
        public bool IsBlocked { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? AvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile? AvatarFile { get; set; }

        [Display(Name = "Xóa ảnh hiện tại")]
        public bool RemoveAvatar { get; set; }

        [Display(Name = "Mật khẩu mới")]
        public string? NewPassword { get; set; }

        [Display(Name = "Xác nhận mật khẩu mới")]
        public string? ConfirmNewPassword { get; set; }

        public bool IsSelf { get; set; }
    }
}
