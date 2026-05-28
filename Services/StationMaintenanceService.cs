using tramsac99.Areas.Admin.Models;

namespace tramsac99.Services
{
    public static class StationMaintenanceService
    {
        public const decimal MonthlyMaintenanceFeeAmount = 10000m;

        // Nghiệp vụ thật: chu kỳ phí duy trì là 1 tháng.
        // Nếu cần test nhanh, đổi tạm thành true để hạn phí đến sau 1 phút.
        public const bool UseOneMinuteDemoCycle = false;
        //public static DateTime GetNextDueDate(DateTime paidAt)
        //{
        //    return paidAt.AddDays(30);
        //}
        public static DateTime GetNextDueDate(DateTime paidAt)
        {
            return UseOneMinuteDemoCycle ? paidAt.AddMinutes(1) : paidAt.AddMonths(1);
        }
        //gia hạn
        public static DateTime GetGraceUntil(DateTime dueDate)
        {
            return UseOneMinuteDemoCycle ? dueDate.AddMinutes(3) : dueDate.AddDays(3);
        }

        public static bool Refresh(ChargingStation station, DateTime? nowValue = null)
        {
            var now = nowValue ?? DateTime.Now;
            var before = Snapshot(station);

            station.MonthlyMaintenanceFee = station.MonthlyMaintenanceFee <= 0 ? MonthlyMaintenanceFeeAmount : station.MonthlyMaintenanceFee;
            station.SystemStatus = StationSystemStatus.Normalize(station.SystemStatus);
            station.OperationStatus = StationOperationStatus.Normalize(string.IsNullOrWhiteSpace(station.OperationStatus) ? station.Status : station.OperationStatus);
            station.Status = StationOperationStatus.ToDisplay(station.OperationStatus);

            if (!station.OwnerUserId.HasValue)
            {
                station.SystemStatus = StationSystemStatus.Approved;
                station.MaintenanceFeeStatus = StationMaintenanceStatus.Active;
                station.MaintenancePaymentStatus = "Không áp dụng";
                station.IsVisible = true;
                station.HiddenReason = null;
                SyncLegacyFields(station);
                return before != Snapshot(station);
            }

            station.MaintenanceFeeStatus = StationMaintenanceStatus.Normalize(station.MaintenanceFeeStatus);

            if (station.SystemStatus == StationSystemStatus.Locked || station.MaintenanceFeeStatus == StationMaintenanceStatus.Locked)
            {
                station.SystemStatus = StationSystemStatus.Locked;
                station.MaintenanceFeeStatus = StationMaintenanceStatus.Locked;
                station.IsVisible = false;
                station.MaintenancePaymentStatus = StationMaintenanceStatus.DisplayLocked;
                station.HiddenReason = string.IsNullOrWhiteSpace(station.HiddenReason) ? "Admin khóa trạm" : station.HiddenReason;
                SyncLegacyFields(station);
                return before != Snapshot(station);
            }

            if (station.SystemStatus != StationSystemStatus.Approved)
            {
                station.IsVisible = false;
                station.HiddenReason = "Trạm chưa được duyệt/kích hoạt";
                SyncLegacyFields(station);
                return before != Snapshot(station);
            }

            if (!station.MaintenanceFeeDueDate.HasValue && station.MaintenancePaidUntil.HasValue)
            {
                station.MaintenanceFeeDueDate = station.MaintenancePaidUntil;
            }

            if (!station.MaintenanceFeePaidAt.HasValue && station.LastMaintenancePaidAt.HasValue)
            {
                station.MaintenanceFeePaidAt = station.LastMaintenancePaidAt;
            }

            if (!station.MaintenanceFeeDueDate.HasValue)
            {
                station.MaintenanceFeeStatus = StationMaintenanceStatus.MaintenanceUnpaid;
                station.MaintenancePaymentStatus = StationMaintenanceStatus.DisplayMaintenanceUnpaid;
                station.MaintenanceFeeGraceUntil ??= GetGraceUntil(now);
                station.IsVisible = false;
                station.HiddenReason = "Chưa thanh toán phí duy trì";
                SyncLegacyFields(station);
                return before != Snapshot(station);
            }

            var dueDate = station.MaintenanceFeeDueDate.Value;
            if (now < dueDate)
            {
                var daysToDue = (dueDate - now).TotalDays;
                station.MaintenanceFeeGraceUntil = null;
                station.HiddenReason = null;
                station.IsVisible = true;
                station.MaintenanceFeeStatus = daysToDue <= 7
                    ? StationMaintenanceStatus.ExpiringSoon
                    : StationMaintenanceStatus.Active;
                station.MaintenancePaymentStatus = daysToDue <= 7
                    ? StationMaintenanceStatus.DisplayExpiringSoon
                    : StationMaintenanceStatus.DisplayActive;
                SyncLegacyFields(station);
                return before != Snapshot(station);
            }

            station.MaintenanceFeeGraceUntil ??= GetGraceUntil(dueDate);
            if (now > station.MaintenanceFeeGraceUntil.Value)
            {
                station.MaintenanceFeeStatus = StationMaintenanceStatus.Hidden;
                station.MaintenancePaymentStatus = StationMaintenanceStatus.DisplayHidden;
                station.IsVisible = false;
                station.HiddenReason = "Quá thời gian gia hạn phí duy trì";
            }
            else
            {
                station.MaintenanceFeeStatus = StationMaintenanceStatus.MaintenanceUnpaid;
                station.MaintenancePaymentStatus = StationMaintenanceStatus.DisplayMaintenanceUnpaid;
                station.IsVisible = false;
                station.HiddenReason = "Quá hạn thanh toán phí duy trì";
            }
            SyncLegacyFields(station);
            return before != Snapshot(station);
        }

        public static void MarkPaid(ChargingStation station, int? paymentId = null, DateTime? paidAtValue = null)
        {
            var paidAt = paidAtValue ?? DateTime.Now;
            var dueDate = GetNextDueDate(paidAt);
            var isLockedByAdmin = StationSystemStatus.Normalize(station.SystemStatus) == StationSystemStatus.Locked
                || StationMaintenanceStatus.Normalize(station.MaintenanceFeeStatus) == StationMaintenanceStatus.Locked;

            station.SystemStatus = isLockedByAdmin ? StationSystemStatus.Locked : StationSystemStatus.Approved;
            station.MonthlyMaintenanceFee = MonthlyMaintenanceFeeAmount;
            station.LastMaintenancePaidAt = paidAt;
            station.MaintenancePaidUntil = dueDate;
            station.MaintenancePaymentStatus = StationMaintenanceStatus.DisplayActive;

            station.MaintenanceFeeStatus = StationMaintenanceStatus.Active;
            station.MaintenanceFeePaidAt = paidAt;
            station.MaintenanceFeeDueDate = dueDate;
            station.MaintenanceFeeGraceUntil = null;
            station.IsVisible = !isLockedByAdmin;
            station.HiddenReason = isLockedByAdmin ? (string.IsNullOrWhiteSpace(station.HiddenReason) ? "Admin khóa trạm" : station.HiddenReason) : null;
            station.LastMaintenancePaymentId = paymentId;
        }

        public static bool IsPubliclyUsable(ChargingStation station)
        {
            station.SystemStatus = StationSystemStatus.Normalize(station.SystemStatus);
            station.OperationStatus = StationOperationStatus.Normalize(string.IsNullOrWhiteSpace(station.OperationStatus) ? station.Status : station.OperationStatus);

            return station.SystemStatus == StationSystemStatus.Approved
                && station.IsVisible
                && StationMaintenanceStatus.IsPubliclyUsable(station.MaintenanceFeeStatus)
                && StationOperationStatus.IsRouteEligible(station.OperationStatus);
        }

        public static int DaysRemaining(ChargingStation station)
        {
            if (!station.MaintenanceFeeDueDate.HasValue)
            {
                return 0;
            }

            return (int)Math.Ceiling((station.MaintenanceFeeDueDate.Value - DateTime.Now).TotalDays);
        }

        private static void SyncLegacyFields(ChargingStation station)
        {
            station.MaintenancePaidUntil = station.MaintenanceFeeDueDate ?? station.MaintenancePaidUntil;
            station.LastMaintenancePaidAt = station.MaintenanceFeePaidAt ?? station.LastMaintenancePaidAt;
        }

        private static string Snapshot(ChargingStation station)
        {
            return string.Join("|", new object?[]
            {
                station.SystemStatus, station.OperationStatus, station.Status,
                station.MaintenanceFeeStatus, station.MaintenancePaymentStatus, station.MaintenanceFeeDueDate,
                station.MaintenanceFeePaidAt, station.MaintenanceFeeGraceUntil, station.IsVisible, station.HiddenReason,
                station.LastMaintenancePaymentId, station.MaintenancePaidUntil, station.LastMaintenancePaidAt, station.MonthlyMaintenanceFee
            });
        }
    }
}
