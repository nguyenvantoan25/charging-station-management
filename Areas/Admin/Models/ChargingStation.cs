using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class ChargingStation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string? Name { get; set; }

        [Required]
        [StringLength(300)]
        public string? Address { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [Required]
        [StringLength(50)]
        public string? Status { get; set; } = ChargingStatus.Active;

        [Column("system_status")]
        [StringLength(30)]
        public string SystemStatus { get; set; } = StationSystemStatus.Approved;

        [Column("operation_status")]
        [StringLength(40)]
        public string OperationStatus { get; set; } = StationOperationStatus.Active;

        [StringLength(100)]
        public string? ChargerType { get; set; }

        [StringLength(50)]
        public string? Power { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerKwh { get; set; }


        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyMaintenanceFee { get; set; } = 10000;

        public DateTime? LastMaintenancePaidAt { get; set; }

        public DateTime? MaintenancePaidUntil { get; set; }

        [StringLength(30)]
        public string MaintenancePaymentStatus { get; set; } = "Chưa đến hạn";

        [Column("maintenance_fee_status")]
        [StringLength(40)]
        public string MaintenanceFeeStatus { get; set; } = StationMaintenanceStatus.Paid;

        [Column("maintenance_fee_due_date")]
        public DateTime? MaintenanceFeeDueDate { get; set; }

        [Column("maintenance_fee_paid_at")]
        public DateTime? MaintenanceFeePaidAt { get; set; }

        [Column("maintenance_fee_grace_until")]
        public DateTime? MaintenanceFeeGraceUntil { get; set; }

        [Column("is_visible")]
        public bool IsVisible { get; set; } = true;

        [Column("hidden_reason")]
        [StringLength(300)]
        public string? HiddenReason { get; set; }

        [Column("last_maintenance_payment_id")]
        public int? LastMaintenancePaymentId { get; set; }

        public ICollection<StationReview> Reviews { get; set; } = new List<StationReview>();

        public ICollection<ChargingPole> ChargingPoles { get; set; } = new List<ChargingPole>(); // Why changed: one station has many poles

        public ICollection<StationActivityLog> ActivityLogs { get; set; } = new List<StationActivityLog>();

        // Why changed: track station ownership so the user can manage only their own stations.
        public int? OwnerUserId { get; set; }

        [ForeignKey(nameof(OwnerUserId))]
        public AppUser? OwnerUser { get; set; }

        [NotMapped]
        public string GoogleMapUrl => $"https://www.google.com/maps?q={Latitude},{Longitude}";

        [NotMapped]
        public string SystemStatusText => StationSystemStatus.ToDisplay(SystemStatus);

        [NotMapped]
        public string OperationStatusText => StationOperationStatus.ToDisplay(OperationStatus);

        [NotMapped]
        public bool IsLockedByAdmin => StationSystemStatus.Normalize(SystemStatus) == StationSystemStatus.Locked
            || StationMaintenanceStatus.Normalize(MaintenanceFeeStatus) == StationMaintenanceStatus.Locked;
    }
}