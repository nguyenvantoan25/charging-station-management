using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.User.ViewModels
{
    public class UpdateReviewHistoryViewModel
    {
        [Required]
        public int ReviewId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }
    }
}
