using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.User.ViewModels
{
    public class ContactSupportRequestViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung cần hỗ trợ.")]
        public string Message { get; set; } = string.Empty;
    }
}
