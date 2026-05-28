using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class ChargingPole
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StationId { get; set; }

        [ForeignKey(nameof(StationId))]
        public ChargingStation? ChargingStation { get; set; }

        [Required]
        [StringLength(50)]
        public string? PoleCode { get; set; }

        [StringLength(100)]
        public string? ChargerType { get; set; }

        [StringLength(50)]
        public string? MaxPower { get; set; }

        [Required]
        [StringLength(50)]
        public string? Status { get; set; } = ChargingStatus.Active;

        [StringLength(500)]
        public string? Note { get; set; }

        public int SortOrder { get; set; }

        public ICollection<ChargingPort> ChargingPorts { get; set; } = new List<ChargingPort>();
    }
}
