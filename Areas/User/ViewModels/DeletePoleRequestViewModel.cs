using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.User.ViewModels
{
    public class DeletePoleRequestViewModel
    {
        [Required]
        public int StationId { get; set; }

        [Required]
        public int PoleId { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }
    }
}
