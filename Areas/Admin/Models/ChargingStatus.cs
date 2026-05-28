namespace tramsac99.Areas.Admin.Models
{
    public static class ChargingStatus
    {
        // Legacy display values used by existing views for station/pole status.
        public const string Active = "Đang hoạt động";
        public const string Inactive = "Không hoạt động";
        public const string Maintenance = "Bảo trì";
        public const string Error = "Lỗi";
        public const string PortAvailable = "Còn chỗ sạc";
        public const string PortBusy = "Đang sử dụng";
        public const string PortInactive = "Không hoạt động";

        public static string NormalizeNodeStatus(string? rawStatus)
        {
            var value = (rawStatus ?? string.Empty).Trim();
            var lower = value.ToLowerInvariant();

            return lower switch
            {
                "bảo trì" or "đang bảo trì" or "maintenance" => Maintenance,
                "lỗi" or "lỗi kỹ thuật" or "quá tải" or "error" or "technical_error" or "overloaded" => Error,
                "không hoạt động" or "tạm ngừng" or "tạm ngừng hoạt động" or "inactive" => Inactive,
                _ => Active
            };
        }

        public static string NormalizePortStatus(string? rawStatus)
        {
            return rawStatus switch
            {
                PortBusy => PortBusy,
                PortInactive => PortInactive,
                _ => PortAvailable
            };
        }

        public static bool IsNodeOperational(string? status)
        {
            return NormalizeNodeStatus(status) == Active;
        }

        public static bool IsPortOperational(string? status)
        {
            return status == PortAvailable || status == PortBusy;
        }

        public static string NormalizeKw(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return string.Empty;

            var normalized = rawValue.Trim()
                .Replace("kw", "", StringComparison.OrdinalIgnoreCase)
                .Replace("kW", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            return $"{normalized} kW";
        }
    }

    public static class StationSystemStatus
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string Locked = "locked";

        public static string Normalize(string? value)
        {
            var input = (value ?? string.Empty).Trim().ToLowerInvariant();
            return input switch
            {
                Pending => Pending,
                Approved => Approved,
                Rejected => Rejected,
                Locked => Locked,
                "chờ duyệt" => Pending,
                "đã duyệt" => Approved,
                "từ chối" => Rejected,
                "bị khóa" => Locked,
                _ => Approved
            };
        }

        public static string ToDisplay(string? value)
        {
            return Normalize(value) switch
            {
                Pending => "Chờ duyệt",
                Rejected => "Từ chối",
                Locked => "Bị khóa bởi admin",
                _ => "Đã duyệt"
            };
        }
    }

    public static class StationOperationStatus
    {
        // Chỉ dùng 4 trạng thái vận hành đồng bộ cho user/admin.
        public const string Active = "active";
        public const string Error = "error";
        public const string Maintenance = "maintenance";
        public const string Inactive = "inactive";

        // Alias legacy để dữ liệu cũ không lỗi sau khi đồng bộ.
        public const string TechnicalError = Error;
        public const string Overloaded = Error;

        public static string Normalize(string? value)
        {
            var input = (value ?? string.Empty).Trim();
            var lower = input.ToLowerInvariant();

            return lower switch
            {
                Active => Active,
                Maintenance => Maintenance,
                Inactive => Inactive,
                Error => Error,
                "technical_error" => Error,
                "overloaded" => Error,
                "hoạt động" => Active,
                "đang hoạt động" => Active,
                "bảo trì" => Maintenance,
                "đang bảo trì" => Maintenance,
                "không hoạt động" => Inactive,
                "tạm ngừng" => Inactive,
                "tạm ngừng hoạt động" => Inactive,
                "lỗi" => Error,
                "lỗi kỹ thuật" => Error,
                "quá tải" => Error,
                _ => Active
            };
        }

        public static string ToDisplay(string? value)
        {
            return Normalize(value) switch
            {
                Maintenance => ChargingStatus.Maintenance,
                Inactive => ChargingStatus.Inactive,
                Error => ChargingStatus.Error,
                _ => ChargingStatus.Active
            };
        }

        public static bool CanBeSelectedByOwner(string? value)
        {
            var normalized = Normalize(value);
            return normalized == Active || normalized == Error || normalized == Maintenance || normalized == Inactive;
        }

        public static bool IsRouteEligible(string? value)
        {
            return Normalize(value) == Active;
        }
    }

    public static class StationMaintenanceStatus
    {
        // New business values.
        public const string Paid = "paid";
        public const string Unpaid = "unpaid";
        public const string Expired = "expired";
        public const string Pending = "pending";

        // Legacy values kept so old data and old screens do not crash.
        public const string Active = "active";
        public const string ExpiringSoon = "expiring_soon";
        public const string MaintenanceUnpaid = "maintenance_unpaid";
        public const string Hidden = "hidden";
        public const string Locked = "locked";

        public const string DisplayPaid = "Đã thanh toán";
        public const string DisplayUnpaid = "Chưa thanh toán";
        public const string DisplayExpired = "Quá hạn";
        public const string DisplayPending = "Đang chờ thanh toán";
        public const string DisplayActive = "Đang hoạt động";
        public const string DisplayExpiringSoon = "Sắp hết hạn";
        public const string DisplayMaintenanceUnpaid = "Chưa thanh toán phí duy trì";
        public const string DisplayHidden = "Tạm ngừng hiển thị";
        public const string DisplayLocked = "Bị khóa";

        public static string Normalize(string? value)
        {
            value = (value ?? string.Empty).Trim();
            var lower = value.ToLowerInvariant();

            return lower switch
            {
                Paid => Paid,
                Unpaid => Unpaid,
                Expired => Expired,
                Pending => Pending,
                Active => Active,
                ExpiringSoon => ExpiringSoon,
                MaintenanceUnpaid => MaintenanceUnpaid,
                Hidden => Hidden,
                Locked => Locked,
                var s when s == DisplayPaid.ToLowerInvariant() => Paid,
                var s when s == DisplayUnpaid.ToLowerInvariant() => Unpaid,
                var s when s == DisplayExpired.ToLowerInvariant() => Expired,
                var s when s == DisplayPending.ToLowerInvariant() => Pending,
                var s when s == DisplayActive.ToLowerInvariant() => Active,
                var s when s == DisplayExpiringSoon.ToLowerInvariant() => ExpiringSoon,
                var s when s == DisplayMaintenanceUnpaid.ToLowerInvariant() => MaintenanceUnpaid,
                var s when s == DisplayHidden.ToLowerInvariant() => Hidden,
                var s when s == DisplayLocked.ToLowerInvariant() => Locked,
                _ => Paid
            };
        }

        public static string ToDisplay(string? value)
        {
            return Normalize(value) switch
            {
                Paid => DisplayPaid,
                Active => DisplayActive,
                Unpaid => DisplayUnpaid,
                Expired => DisplayExpired,
                Pending => DisplayPending,
                ExpiringSoon => DisplayExpiringSoon,
                MaintenanceUnpaid => DisplayMaintenanceUnpaid,
                Hidden => DisplayHidden,
                Locked => DisplayLocked,
                _ => DisplayPaid
            };
        }

        public static bool IsPubliclyUsable(string? value)
        {
            var normalized = Normalize(value);
            return normalized == Paid || normalized == Active || normalized == ExpiringSoon;
        }

        public static bool CanPayMaintenance(string? value)
        {
            var normalized = Normalize(value);
            return normalized == Unpaid || normalized == Expired || normalized == Pending || normalized == MaintenanceUnpaid || normalized == Hidden || normalized == ExpiringSoon;
        }
    }

    public static class StationActivityActionType
    {
        public const string UpdateOperationStatus = "update_operation_status";
        public const string AddChargingPole = "add_charging_pole";
        public const string UpdateChargingPole = "update_charging_pole";
        public const string DeleteChargingPole = "delete_charging_pole";
        public const string UpdateStationInfo = "update_station_info";
        public const string RegistrationPaid = "registration_paid";
        public const string MaintenancePaid = "maintenance_paid";
        public const string AdminLock = "admin_lock";
        public const string AdminUnlock = "admin_unlock";
        public const string AdminHide = "admin_hide";
        public const string AdminShow = "admin_show";
    }
}
