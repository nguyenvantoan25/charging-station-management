namespace tramsac99.Areas.Admin.Models.Dto
{
    public class ChargingStationDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Status { get; set; }
        public string? SystemStatus { get; set; }
        public string? SystemStatusText { get; set; }
        public string? OperationStatus { get; set; }
        public string? OperationStatusText { get; set; }
        public string? ChargerType { get; set; }
        public string? Power { get; set; }
        public decimal PricePerKwh { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public int PoleCount { get; set; }
        public int PortCount { get; set; }
        public int ActivePoleCount { get; set; }
        public int ActivePortCount { get; set; }
        public int? OwnerUserId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public bool IsAdminManaged { get; set; }
        public decimal MonthlyMaintenanceFee { get; set; }
        public DateTime? MaintenancePaidUntil { get; set; }
        public string? MaintenancePaymentStatus { get; set; }
        public bool IsMaintenanceDue { get; set; }
        public string? MaintenanceFeeStatus { get; set; }
        public string? MaintenanceFeeStatusText { get; set; }
        public DateTime? MaintenanceFeeDueDate { get; set; }
        public DateTime? MaintenanceFeePaidAt { get; set; }
        public DateTime? MaintenanceFeeGraceUntil { get; set; }
        public bool IsVisible { get; set; }
        public string? HiddenReason { get; set; }
        public int? LastMaintenancePaymentId { get; set; }
        public int DaysRemaining { get; set; }
        public string SourceLabel => IsAdminManaged ? "Admin thêm trực tiếp" : "User gửi yêu cầu";
    }
}
