using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.User.ViewModels
{
    public class AddPoleRequestViewModel
    {
        [Required]
        public int StationId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã trụ.")]
        public string PoleCode { get; set; } = string.Empty;

        public string? MaxPower { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }
    }
}
