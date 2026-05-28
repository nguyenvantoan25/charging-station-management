using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class RouteHistory
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(200)]
        public string RouteName { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        public string StartName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? StartAddress { get; set; }

        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }

        [Required]
        [StringLength(250)]
        public string EndName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? EndAddress { get; set; }

        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }

        public double TotalDistanceKm { get; set; }
        public double FirstLegKm { get; set; }

        public int VehicleRangeKm { get; set; }
        public int StartBattery { get; set; }
        public int ReserveBattery { get; set; }
        public int MaxDetourKm { get; set; }
        public int StopCount { get; set; }

        public string? StopsJson { get; set; }
        public string? RoutePathJson { get; set; }

        public bool IsFavorite { get; set; }
        public bool IsShared { get; set; }

        [StringLength(80)]
        public string? ShareToken { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? LastUsedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public AppUser? User { get; set; }
    }
}
