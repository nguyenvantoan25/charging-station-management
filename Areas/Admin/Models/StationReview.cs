using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class StationReview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StationId { get; set; }

        [ForeignKey("StationId")]
        public ChargingStation? ChargingStation { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        [Required]
        [StringLength(100)]
        public string? UserName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Why changed: track the last edit time so dashboard can show updated reviews as recent activity.
        public DateTime? UpdatedAt { get; set; }
    }
}
