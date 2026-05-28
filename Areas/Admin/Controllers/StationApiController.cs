using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.Admin.Models.Dto;
using tramsac99.Data;
using tramsac99.Services;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/api/stations")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StationApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ChargingHierarchyService _hierarchyService;

        public StationApiController(AppDbContext context, ChargingHierarchyService hierarchyService)
        {
            _context = context;
            _hierarchyService = hierarchyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStations()
        {
            await RefreshMaintenanceStatusesAsync();

            var stations = await _context.ChargingStations
                .Include(s => s.OwnerUser)
                .Include(s => s.Reviews)
                .Include(s => s.ChargingPoles)
                    .ThenInclude(p => p.ChargingPorts)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var result = stations.Select(BuildStationDto).ToList();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStationById(int id)
        {
            var station = await _context.ChargingStations
                .Include(s => s.OwnerUser)
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    address = s.Address,
                    latitude = s.Latitude,
                    longitude = s.Longitude,
                    status = StationOperationStatus.ToDisplay(s.OperationStatus),
                    systemStatus = s.SystemStatus,
                    systemStatusText = StationSystemStatus.ToDisplay(s.SystemStatus),
                    operationStatus = s.OperationStatus,
                    operationStatusText = StationOperationStatus.ToDisplay(s.OperationStatus),
                    ownerUserId = s.OwnerUserId,
                    ownerName = s.OwnerUser != null ? (s.OwnerUser.FullName ?? s.OwnerUser.Username) : "Admin",
                    isAdminManaged = !s.OwnerUserId.HasValue,
                    monthlyMaintenanceFee = s.MonthlyMaintenanceFee,
                    maintenancePaidUntil = s.MaintenancePaidUntil,
                    maintenancePaymentStatus = !s.OwnerUserId.HasValue ? "Không áp dụng" : s.MaintenancePaymentStatus,
                    isMaintenanceDue = s.OwnerUserId.HasValue && (!s.MaintenanceFeeDueDate.HasValue || s.MaintenanceFeeDueDate.Value <= DateTime.Now),
                    maintenanceFeeStatus = s.MaintenanceFeeStatus,
                    maintenanceFeeStatusText = StationMaintenanceStatus.ToDisplay(s.MaintenanceFeeStatus),
                    maintenanceFeeDueDate = s.MaintenanceFeeDueDate,
                    maintenanceFeePaidAt = s.MaintenanceFeePaidAt,
                    maintenanceFeeGraceUntil = s.MaintenanceFeeGraceUntil,
                    isVisible = s.IsVisible,
                    hiddenReason = s.HiddenReason
                })
                .FirstOrDefaultAsync();

            if (station == null)
                return NotFound(new { message = "Không tìm thấy trạm." });

            return Ok(station);
        }

        [HttpPost]
        public async Task<IActionResult> CreateStation([FromBody] ChargingStation newStation)
        {
            if (newStation == null)
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            try
            {
                newStation.Name = newStation.Name?.Trim();
                newStation.Address = newStation.Address?.Trim();
                newStation.Status = NormalizeStationStatus4(newStation.Status);
                newStation.SystemStatus = StationSystemStatus.Approved;
                newStation.OperationStatus = StationOperationStatus.Normalize(newStation.Status);

                // Why changed: admin-created stations stay unmanaged by any user account.
                newStation.OwnerUserId = null;
                newStation.ChargerType = null;
                newStation.Power = null;
                newStation.PricePerKwh = 0;
                newStation.MaintenanceFeeStatus = StationMaintenanceStatus.Paid;
                newStation.MaintenancePaymentStatus = "Không áp dụng";
                newStation.IsVisible = true;
                newStation.HiddenReason = null;

                if (string.IsNullOrWhiteSpace(newStation.Name) || string.IsNullOrWhiteSpace(newStation.Address))
                    return BadRequest(new { message = "Vui lòng nhập tên trạm và địa chỉ." });

                if (double.IsNaN(newStation.Latitude) || double.IsNaN(newStation.Longitude))
                    return BadRequest(new { message = "Vĩ độ hoặc kinh độ không hợp lệ." });

                _context.ChargingStations.Add(newStation);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Thêm trạm thành công." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = $"Lỗi lưu dữ liệu trạm: {GetInnermostMessage(ex)}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi thêm trạm: {GetInnermostMessage(ex)}" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStation(int id, [FromBody] ChargingStation updatedStation)
        {
            if (updatedStation == null)
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            try
            {
                var station = await _context.ChargingStations
                    .Include(x => x.ChargingPoles)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (station == null)
                    return NotFound(new { message = "Không tìm thấy trạm." });

                var name = updatedStation.Name?.Trim();
                var address = updatedStation.Address?.Trim();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
                    return BadRequest(new { message = "Vui lòng nhập tên trạm và địa chỉ." });

                station.Name = name;
                station.Address = address;
                station.Latitude = updatedStation.Latitude;
                station.Longitude = updatedStation.Longitude;
                var oldOperationStatus = station.OperationStatus;
                station.Status = NormalizeStationStatus4(updatedStation.Status);
                station.OperationStatus = StationOperationStatus.Normalize(station.Status);

                if (station.Status == ChargingStatus.Inactive ||
                    station.Status == ChargingStatus.Maintenance ||
                    station.Status == ChargingStatus.Error)
                {
                    foreach (var pole in station.ChargingPoles)
                    {
                        pole.Status = station.Status;
                    }
                }

                if (oldOperationStatus != station.OperationStatus)
                {
                    _context.StationActivityLogs.Add(new StationActivityLog
                    {
                        StationId = station.Id,
                        UserId = null,
                        ActionType = StationActivityActionType.UpdateOperationStatus,
                        OldValue = oldOperationStatus,
                        NewValue = station.OperationStatus,
                        Description = "Admin cập nhật trạng thái vận hành trạm.",
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Cập nhật trạm thành công." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = $"Lỗi cập nhật dữ liệu trạm: {GetInnermostMessage(ex)}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi cập nhật trạm: {GetInnermostMessage(ex)}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var station = await _context.ChargingStations
                .Include(x => x.OwnerUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (station == null)
                return NotFound(new { message = "Không tìm thấy trạm." });

            var now = DateTime.Now;
            var stationName = station.Name?.Trim() ?? $"Trạm #{station.Id}";
            var isUserSubmitted = station.OwnerUserId.HasValue;

            if (isUserSubmitted)
            {
                var owner = station.OwnerUser;
                if (owner != null)
                {
                    _context.SupportRequests.Add(new SupportRequest
                    {
                        SenderUserId = owner.Id,
                        SenderUserName = owner.Username,
                        FullName = string.IsNullOrWhiteSpace(owner.FullName) ? owner.Username : owner.FullName.Trim(),
                        Email = owner.Email,
                        PhoneNumber = null,
                        Subject = "Thông báo xóa trạm sạc từ admin",
                        Message = $"Admin đã xóa trạm sạc '{stationName}' khỏi hệ thống. Nếu bạn cần hỗ trợ hoặc muốn gửi lại hồ sơ, vui lòng vào mục Liên hệ để trao đổi với admin.",
                        Status = "Đã xử lý",
                        IsRead = true,
                        CreatedAt = now,
                        ReadAt = now,
                        ResolvedAt = now,
                        AdminReply = $"Trạm '{stationName}' đã bị xóa khỏi hệ thống bởi admin.",
                        LastStatusChangedAt = now,
                        IsUserSeen = false,
                        UserSeenAt = null
                    });
                }

                var relatedRequests = await _context.StationRegistrationRequests
                    .Where(x => x.CreatedStationId == station.Id)
                    .ToListAsync();

                foreach (var request in relatedRequests)
                {
                    request.AdminNote = $"Admin đã xóa trạm '{stationName}' khỏi hệ thống vào {now:dd/MM/yyyy HH:mm}.";
                }
            }

            _context.ChargingStations.Remove(station);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isUserSubmitted
                    ? "Đã xóa trạm và gửi thông báo về user trong mục Liên hệ."
                    : "Xóa trạm thành công."
            });
        }

        [HttpPost("{id}/lock")]
        public async Task<IActionResult> LockStation(int id)
        {
            var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == id);
            if (station == null)
            {
                return NotFound(new { message = "Không tìm thấy trạm." });
            }

            var oldSystemStatus = station.SystemStatus;
            station.SystemStatus = StationSystemStatus.Locked;
            station.MaintenanceFeeStatus = StationMaintenanceStatus.Locked;
            station.MaintenancePaymentStatus = StationMaintenanceStatus.DisplayLocked;
            station.IsVisible = false;
            station.HiddenReason = "Admin khóa thủ công";
            LogAdminStationActivity(station.Id, StationActivityActionType.AdminLock, oldSystemStatus, StationSystemStatus.Locked, station.HiddenReason);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã khóa trạm. Trạm không còn hiển thị với người dùng thường." });
        }

        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> UnlockStation(int id)
        {
            var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == id);
            if (station == null)
            {
                return NotFound(new { message = "Không tìm thấy trạm." });
            }

            var oldSystemStatus = station.SystemStatus;
            station.SystemStatus = StationSystemStatus.Approved;
            station.MaintenanceFeeStatus = StationMaintenanceStatus.Paid;
            station.HiddenReason = null;
            StationMaintenanceService.Refresh(station);
            LogAdminStationActivity(station.Id, StationActivityActionType.AdminUnlock, oldSystemStatus, StationSystemStatus.Approved, "Admin mở khóa trạm.");
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã mở khóa trạm và cập nhật lại trạng thái phí duy trì." });
        }

        [HttpPost("{id}/hide")]
        public async Task<IActionResult> HideStation(int id)
        {
            var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == id);
            if (station == null)
            {
                return NotFound(new { message = "Không tìm thấy trạm." });
            }

            station.IsVisible = false;
            station.HiddenReason = "Admin tạm ẩn thủ công";
            LogAdminStationActivity(station.Id, StationActivityActionType.AdminHide, null, "is_visible=false", station.HiddenReason);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã tạm ẩn trạm khỏi người dùng thường." });
        }

        [HttpPost("{id}/show")]
        public async Task<IActionResult> ShowStation(int id)
        {
            var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == id);
            if (station == null)
            {
                return NotFound(new { message = "Không tìm thấy trạm." });
            }

            station.MaintenanceFeeStatus = StationMaintenanceStatus.Paid;
            station.IsVisible = true;
            station.HiddenReason = null;
            StationMaintenanceService.Refresh(station);
            LogAdminStationActivity(station.Id, StationActivityActionType.AdminShow, null, "is_visible=true", "Admin mở hiển thị trạm.");
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã mở hiển thị trạm nếu còn đủ điều kiện phí duy trì." });
        }

        [HttpGet("{id}/activity-logs")]
        public async Task<IActionResult> GetStationActivityLogs(int id)
        {
            var exists = await _context.ChargingStations.AnyAsync(x => x.Id == id);
            if (!exists)
            {
                return NotFound(new { message = "Không tìm thấy trạm." });
            }

            var logs = await _context.StationActivityLogs
                .Where(x => x.StationId == id)
                .Include(x => x.User)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .Select(x => new
                {
                    x.Id,
                    x.StationId,
                    x.UserId,
                    userName = x.User != null ? (x.User.FullName ?? x.User.Username) : "Admin/Hệ thống",
                    x.ActionType,
                    x.OldValue,
                    x.NewValue,
                    x.Description,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }

        private void LogAdminStationActivity(int stationId, string actionType, string? oldValue, string? newValue, string? description)
        {
            _context.StationActivityLogs.Add(new StationActivityLog
            {
                StationId = stationId,
                UserId = null,
                ActionType = actionType,
                OldValue = oldValue,
                NewValue = newValue,
                Description = description,
                CreatedAt = DateTime.Now
            });
        }

        private static ChargingStationDto BuildStationDto(ChargingStation s)
        {
            var maintenanceStatus = MapMaintenanceStatusForAdmin(s);
            var isAdminManaged = !s.OwnerUserId.HasValue;
            var ownerName = isAdminManaged
                ? "Admin"
                : (s.OwnerUser != null
                    ? (!string.IsNullOrWhiteSpace(s.OwnerUser.FullName) ? s.OwnerUser.FullName : s.OwnerUser.Username)
                    : "User");

            return new ChargingStationDto
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Status = StationOperationStatus.ToDisplay(s.OperationStatus),
                SystemStatus = s.SystemStatus,
                SystemStatusText = StationSystemStatus.ToDisplay(s.SystemStatus),
                OperationStatus = s.OperationStatus,
                OperationStatusText = StationOperationStatus.ToDisplay(s.OperationStatus),
                ChargerType = s.ChargerType,
                Power = s.Power,
                PricePerKwh = s.PricePerKwh,
                AverageRating = s.Reviews.Any() ? Math.Round(s.Reviews.Average(r => (double)r.Rating), 1) : 0,
                ReviewCount = s.Reviews.Count,
                PoleCount = s.ChargingPoles.Count,
                ActivePoleCount = s.ChargingPoles.Count(p => p.Status == ChargingStatus.Active || p.Status == "Hoạt động"),
                PortCount = s.ChargingPoles.SelectMany(p => p.ChargingPorts).Count(),
                ActivePortCount = s.ChargingPoles.SelectMany(p => p.ChargingPorts).Count(p => p.Status == ChargingStatus.Active || p.Status == "Hoạt động"),
                OwnerUserId = s.OwnerUserId,
                OwnerName = ownerName,
                OwnerPhone = null,
                IsAdminManaged = isAdminManaged,
                MonthlyMaintenanceFee = s.MonthlyMaintenanceFee,
                MaintenancePaidUntil = s.MaintenancePaidUntil,
                MaintenancePaymentStatus = isAdminManaged ? "Không áp dụng" : s.MaintenancePaymentStatus,
                IsMaintenanceDue = s.OwnerUserId.HasValue && (!s.MaintenanceFeeDueDate.HasValue || s.MaintenanceFeeDueDate.Value <= DateTime.Now),
                MaintenanceFeeStatus = isAdminManaged ? "not_applicable" : maintenanceStatus,
                MaintenanceFeeStatusText = isAdminManaged ? "Không áp dụng" : GetMaintenanceStatusTextForAdmin(maintenanceStatus),
                MaintenanceFeeDueDate = s.MaintenanceFeeDueDate,
                MaintenanceFeePaidAt = s.MaintenanceFeePaidAt,
                MaintenanceFeeGraceUntil = s.MaintenanceFeeGraceUntil,
                IsVisible = s.IsVisible,
                HiddenReason = s.HiddenReason,
                LastMaintenancePaymentId = s.LastMaintenancePaymentId,
                DaysRemaining = StationMaintenanceService.DaysRemaining(s)
            };
        }

        private static string MapMaintenanceStatusForAdmin(ChargingStation station)
        {
            if (station.SystemStatus == StationSystemStatus.Locked || StationMaintenanceStatus.Normalize(station.MaintenanceFeeStatus) == StationMaintenanceStatus.Locked)
            {
                return StationMaintenanceStatus.Locked;
            }

            var normalized = StationMaintenanceStatus.Normalize(station.MaintenanceFeeStatus);
            if (!station.IsVisible && normalized != StationMaintenanceStatus.Paid && normalized != StationMaintenanceStatus.ExpiringSoon)
            {
                if (normalized == StationMaintenanceStatus.Expired || normalized == StationMaintenanceStatus.Unpaid || normalized == StationMaintenanceStatus.MaintenanceUnpaid)
                {
                    return StationMaintenanceStatus.Hidden;
                }
            }

            if (station.MaintenanceFeeDueDate.HasValue)
            {
                var days = Math.Ceiling((station.MaintenanceFeeDueDate.Value - DateTime.Now).TotalDays);
                if (days >= 0 && days <= 7 && (normalized == StationMaintenanceStatus.Paid || normalized == StationMaintenanceStatus.Active))
                {
                    return StationMaintenanceStatus.ExpiringSoon;
                }
            }

            return normalized switch
            {
                StationMaintenanceStatus.Paid => StationMaintenanceStatus.Active,
                StationMaintenanceStatus.Active => StationMaintenanceStatus.Active,
                StationMaintenanceStatus.ExpiringSoon => StationMaintenanceStatus.ExpiringSoon,
                StationMaintenanceStatus.Unpaid => StationMaintenanceStatus.MaintenanceUnpaid,
                StationMaintenanceStatus.Expired => StationMaintenanceStatus.MaintenanceUnpaid,
                StationMaintenanceStatus.Pending => StationMaintenanceStatus.MaintenanceUnpaid,
                StationMaintenanceStatus.MaintenanceUnpaid => StationMaintenanceStatus.MaintenanceUnpaid,
                StationMaintenanceStatus.Hidden => StationMaintenanceStatus.Hidden,
                StationMaintenanceStatus.Locked => StationMaintenanceStatus.Locked,
                _ => StationMaintenanceStatus.Active
            };
        }

        private static string GetMaintenanceStatusTextForAdmin(string status)
        {
            return status switch
            {
                StationMaintenanceStatus.Active => StationMaintenanceStatus.DisplayActive,
                StationMaintenanceStatus.ExpiringSoon => StationMaintenanceStatus.DisplayExpiringSoon,
                StationMaintenanceStatus.MaintenanceUnpaid => StationMaintenanceStatus.DisplayMaintenanceUnpaid,
                StationMaintenanceStatus.Hidden => StationMaintenanceStatus.DisplayHidden,
                StationMaintenanceStatus.Locked => StationMaintenanceStatus.DisplayLocked,
                _ => StationMaintenanceStatus.ToDisplay(status)
            };
        }

        private async Task RefreshMaintenanceStatusesAsync()
        {
            var stations = await _context.ChargingStations
                .ToListAsync();

            var changed = false;
            foreach (var station in stations)
            {
                if (StationMaintenanceService.Refresh(station))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        private static string NormalizeStationStatus4(string? value)
        {
            return ChargingStatus.NormalizeNodeStatus(value);
        }

        private static string GetInnermostMessage(Exception ex)
        {
            var current = ex;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.Message;
        }
    }
}
