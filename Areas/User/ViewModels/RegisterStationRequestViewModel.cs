using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class RegisterStationRequestViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên trạm.")]
        [Display(Name = "Tên trạm")]
        public string StationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên đơn vị vận hành.")]
        [Display(Name = "Đơn vị vận hành")]
        public string OperatorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email liên hệ.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email liên hệ")]
        public string ContactEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Display(Name = "Số điện thoại")]
        public string ContactPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Vĩ độ")]
        public double Latitude { get; set; }

        [Display(Name = "Kinh độ")]
        public double Longitude { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Ảnh trạm")]
        public IFormFile? ImageFile { get; set; }

        [Range(1, 100, ErrorMessage = "Số lượng trụ dự kiến phải từ 1 đến 100.")]
        [Display(Name = "Số lượng trụ dự kiến")]
        public int InitialPoleCount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại sạc.")]
        [Display(Name = "Loại sạc dự kiến")]
        public string InitialPoleChargerType { get; set; } = string.Empty;

        [Display(Name = "Công suất trụ dự kiến")]
        public string? InitialPoleMaxPower { get; set; }

        [Display(Name = "Ghi chú về trụ")]
        public string? InitialPoleNote { get; set; }


        public const decimal RegistrationFee = 50000m;

        [Display(Name = "Phí xác nhận dự kiến")]
        public decimal EstimatedFeeAmount => RegistrationFee;

        // Why changed: reuse the page to re-render request progress after submit.
        public StationRegistrationRequest? CurrentRequest { get; set; }
    }
}
