using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    [Table("station_activity_logs")]
    public class StationActivityLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("station_id")]
        public int StationId { get; set; }

        [ForeignKey(nameof(StationId))]
        public ChargingStation? Station { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public AppUser? User { get; set; }

        [Required]
        [Column("action_type")]
        [StringLength(80)]
        public string ActionType { get; set; } = string.Empty;

        [Column("old_value")]
        [StringLength(1000)]
        public string? OldValue { get; set; }

        [Column("new_value")]
        [StringLength(1000)]
        public string? NewValue { get; set; }

        [Column("description")]
        [StringLength(1000)]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
