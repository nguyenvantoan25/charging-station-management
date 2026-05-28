using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.Admin.Models
{
    public class StationOperationRequest
    {
        [Key]
        public int Id { get; set; }

        public int StationId { get; set; }
        public ChargingStation? Station { get; set; }

        public int UserId { get; set; }
        public AppUser? User { get; set; }

        [Required]
        [StringLength(50)]
        public string RequestType { get; set; } = StationOperationRequestType.StatusUpdate;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = StationWorkflowStatus.Pending;

        [StringLength(50)]
        public string? RequestedStationStatus { get; set; }

        // Why changed: keep target pole identity for update/delete approval.
        public int? PoleId { get; set; }

        [StringLength(50)]
        public string? PoleCode { get; set; }

        [StringLength(50)]
        public string? PoleMaxPower { get; set; }

        // Why changed: allow admin to apply the requested pole status.
        [StringLength(50)]
        public string? RequestedPoleStatus { get; set; }

        [StringLength(1000)]
        public string? UserNote { get; set; }

        [StringLength(1000)]
        public string? AdminNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ReviewedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
