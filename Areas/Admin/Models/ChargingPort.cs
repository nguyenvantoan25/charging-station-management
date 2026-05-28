using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class ChargingPort
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PoleId { get; set; }

        [ForeignKey(nameof(PoleId))]
        public ChargingPole? ChargingPole { get; set; }

        [Required]
        [StringLength(50)]
        public string? PortCode { get; set; } // Why changed: identify each port inside a pole

        [StringLength(100)]
        public string? ConnectorType { get; set; }

        [StringLength(50)]
        public string? MaxPower { get; set; }

        [Required]
        [StringLength(50)]
        public string? Status { get; set; } = ChargingStatus.Active;

        [StringLength(500)]
        public string? Note { get; set; }

        public int SortOrder { get; set; }
    }
}