using System.ComponentModel.DataAnnotations;
using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class StationStatusRequestViewModel
    {
        [Required]
        public int StationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
        public string RequestedStatus { get; set; } = ChargingStatus.Active;

        [StringLength(1000)]
        public string? Note { get; set; }
    }
}
