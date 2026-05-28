using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tramsac99.Areas.Admin.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? StationId { get; set; }

        public int? RegistrationRequestId { get; set; }

        [Required]
        [StringLength(40)]
        public string PaymentType { get; set; } = PaymentTransactionType.InitialRegistration;

        [Required]
        [StringLength(40)]
        public string Status { get; set; } = PaymentTransactionStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public long? PayOsOrderCode { get; set; }

        [StringLength(500)]
        public string? PayOsCheckoutUrl { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public AppUser? User { get; set; }

        [ForeignKey(nameof(StationId))]
        public ChargingStation? Station { get; set; }

        [ForeignKey(nameof(RegistrationRequestId))]
        public StationRegistrationRequest? RegistrationRequest { get; set; }
    }

    public static class PaymentTransactionType
    {
        public const string InitialRegistration = "registration_fee";
        public const string Maintenance = "maintenance_fee";

        public const string LegacyInitialRegistration = "Phí kích hoạt trạm";
        public const string LegacyMaintenance = "Phí duy trì trạm";

        public static string ToDisplay(string? value)
        {
            return value switch
            {
                InitialRegistration or LegacyInitialRegistration => "Phí đăng ký ban đầu",
                Maintenance or LegacyMaintenance => "Phí duy trì trạm",
                _ => string.IsNullOrWhiteSpace(value) ? "Giao dịch" : value!
            };
        }

        public static bool Matches(string? actual, string expected)
        {
            return expected switch
            {
                InitialRegistration => actual == InitialRegistration || actual == LegacyInitialRegistration,
                Maintenance => actual == Maintenance || actual == LegacyMaintenance,
                _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    public static class PaymentTransactionStatus
    {
        public const string Pending = "Đang chờ thanh toán";
        public const string Paid = "Đã thanh toán";
        public const string Cancelled = "Đã hủy thanh toán";
        public const string Failed = "Thanh toán chưa thành công";
    }
}
