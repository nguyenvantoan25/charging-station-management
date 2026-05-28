using System.ComponentModel.DataAnnotations;
using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class UpdatePoleRequestViewModel
    {
        [Required]
        public int StationId { get; set; }

        [Required]
        public int PoleId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã trụ.")]
        public string PoleCode { get; set; } = string.Empty;

        public string? MaxPower { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái trụ.")]
        public string RequestedStatus { get; set; } = ChargingStatus.Active;

        [StringLength(1000)]
        public string? Note { get; set; }
    }
}
