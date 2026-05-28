using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class FavoriteStation
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int StationId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(UserId))]
        public AppUser? User { get; set; }

        [ForeignKey(nameof(StationId))]
        public ChargingStation? ChargingStation { get; set; }
    }
}
