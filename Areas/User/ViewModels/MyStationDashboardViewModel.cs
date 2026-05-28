using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class MyStationDashboardViewModel
    {
        public List<ChargingStation> Stations { get; set; } = new();
        public List<StationRegistrationRequest> RegistrationRequests { get; set; } = new();
        public List<StationOperationRequest> OperationRequests { get; set; } = new();

        public int TotalOwnedStations => Stations.Count;
        public int PendingRegistrations => RegistrationRequests.Count(x => x.ApprovalStatus == StationWorkflowStatus.Pending || x.ApprovalStatus == StationWorkflowStatus.Approved || x.ApprovalStatus == StationWorkflowStatus.AwaitingPayment);
        public int PendingOperations => OperationRequests.Count(x => x.Status == StationWorkflowStatus.Pending);
        public int ActivePoles => Stations.Sum(x => x.ChargingPoles.Count(p => p.Status == ChargingStatus.Active));
        public int ActiveStations => Stations.Count(x => StationOperationStatus.Normalize(x.OperationStatus) == StationOperationStatus.Active);
        public int MaintenanceStations => Stations.Count(x => StationOperationStatus.Normalize(x.OperationStatus) == StationOperationStatus.Maintenance);
        public int LockedStations => Stations.Count(x => StationSystemStatus.Normalize(x.SystemStatus) == StationSystemStatus.Locked || StationMaintenanceStatus.Normalize(x.MaintenanceFeeStatus) == StationMaintenanceStatus.Locked);
        public int MaintenanceDueStations => Stations.Count(x => StationMaintenanceStatus.CanPayMaintenance(x.MaintenanceFeeStatus));
        public int ExpiringSoonStations => Stations.Count(x => x.MaintenanceFeeStatus == StationMaintenanceStatus.ExpiringSoon);
        public int HiddenStations => Stations.Count(x => x.MaintenanceFeeStatus == StationMaintenanceStatus.Hidden || x.MaintenanceFeeStatus == StationMaintenanceStatus.Expired || !x.IsVisible);
    }
}
