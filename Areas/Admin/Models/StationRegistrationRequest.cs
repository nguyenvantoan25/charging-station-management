using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.Admin.Models
{
    public class StationRegistrationRequest
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public AppUser? User { get; set; }

        [Required]
        [StringLength(200)]
        public string StationName { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string OperatorName { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string ContactEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string ContactPhone { get; set; } = string.Empty;

        [Required]
        [StringLength(300)]
        public string Address { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(300)]
        public string? ImageUrl { get; set; }

        public int InitialPoleCount { get; set; }

        [StringLength(100)]
        public string? InitialPoleChargerType { get; set; }

        [StringLength(50)]
        public string? InitialPoleMaxPower { get; set; }

        [StringLength(1000)]
        public string? InitialPoleNote { get; set; }

        [Required]
        [StringLength(30)]
        public string ApprovalStatus { get; set; } = StationWorkflowStatus.Pending;

        [Required]
        [StringLength(30)]
        public string PaymentStatus { get; set; } = "Chưa thanh toán";

        public long? PayOsOrderCode { get; set; }

        [StringLength(500)]
        public string? PayOsCheckoutUrl { get; set; }

        public decimal FeeAmount { get; set; } = 50000;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ReviewedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [StringLength(1000)]
        public string? AdminNote { get; set; }

        public int? CreatedStationId { get; set; }
        public ChargingStation? CreatedStation { get; set; }
    }
}
