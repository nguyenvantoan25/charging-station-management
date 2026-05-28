using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class ManageStationDetailsViewModel
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Status { get; set; } = ChargingStatus.Active;
        public string SystemStatus { get; set; } = StationSystemStatus.Approved;
        public string SystemStatusText => StationSystemStatus.ToDisplay(SystemStatus);
        public string OperationStatus { get; set; } = StationOperationStatus.Active;
        public string OperationStatusText => StationOperationStatus.ToDisplay(OperationStatus);
        public bool IsLockedByAdmin => StationSystemStatus.Normalize(SystemStatus) == StationSystemStatus.Locked || StationMaintenanceStatus.Normalize(MaintenanceFeeStatus) == StationMaintenanceStatus.Locked;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string PhoneNumber { get; set; } = "-";
        public int TotalPoleCount { get; set; }
        public decimal MonthlyMaintenanceFee { get; set; } = 10000;
        public DateTime? LastMaintenancePaidAt { get; set; }
        public DateTime? MaintenancePaidUntil { get; set; }
        public string MaintenancePaymentStatus { get; set; } = "Chưa đến hạn";
        public string MaintenanceFeeStatus { get; set; } = StationMaintenanceStatus.Paid;
        public string MaintenanceFeeStatusText => StationMaintenanceStatus.ToDisplay(MaintenanceFeeStatus);
        public DateTime? MaintenanceFeeDueDate { get; set; }
        public DateTime? MaintenanceFeePaidAt { get; set; }
        public DateTime? MaintenanceFeeGraceUntil { get; set; }
        public bool IsVisible { get; set; } = true;
        public string? HiddenReason { get; set; }
        public int DaysRemaining => MaintenanceFeeDueDate.HasValue ? (int)Math.Ceiling((MaintenanceFeeDueDate.Value - DateTime.Now).TotalDays) : 0;
        public bool IsMaintenanceDue => !MaintenanceFeeDueDate.HasValue || MaintenanceFeeDueDate.Value <= DateTime.Now;
        public bool CanPayMaintenance => StationMaintenanceStatus.CanPayMaintenance(MaintenanceFeeStatus);
        public bool CanManageOperations => StationSystemStatus.Normalize(SystemStatus) == StationSystemStatus.Approved && !IsLockedByAdmin;
        public DateTime? LatestOperationAt { get; set; }
        public List<PoleItemViewModel> Poles { get; set; } = new();
        public List<StationOperationRequest> OperationRequests { get; set; } = new();

        // Why changed: add server-side paging for request history so the right panel does not become too long.
        public List<StationOperationRequest> HistoryItems { get; set; } = new();
        public int HistoryPage { get; set; } = 1;
        public int HistoryPageSize { get; set; } = 4;
        public int HistoryTotalCount { get; set; }
        public int HistoryTotalPages => Math.Max(1, (int)Math.Ceiling(HistoryTotalCount / (double)Math.Max(1, HistoryPageSize)));

        // Why changed: add server-side paging for poles so the pole list remains compact.
        public List<PoleItemViewModel> PoleItems { get; set; } = new();
        public int PolePage { get; set; } = 1;
        public int PolePageSize { get; set; } = 4;
        public int PoleTotalCount { get; set; }
        public int PoleTotalPages => Math.Max(1, (int)Math.Ceiling(PoleTotalCount / (double)Math.Max(1, PolePageSize)));
    }
}

