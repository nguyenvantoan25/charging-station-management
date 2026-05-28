using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.Admin.Models.Dto
{
    public class CreateReviewRequest
    {
        [Required]
        public int StationId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(100)]
        public string ?UserName { get; set; }

        [StringLength(1000)]
        public string ?Comment { get; set; }
    }
}