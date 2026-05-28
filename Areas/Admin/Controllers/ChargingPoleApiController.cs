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
    [Route("Admin/api/poles")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ChargingPoleApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ChargingHierarchyService _hierarchyService;

        public ChargingPoleApiController(AppDbContext context, ChargingHierarchyService hierarchyService)
        {
            _context = context;
            _hierarchyService = hierarchyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPoles([FromQuery] int? stationId)
        {
            try
            {
                var query = _context.ChargingPoles.AsNoTracking().AsQueryable();

                if (stationId.HasValue)
                {
                    query = query.Where(x => x.StationId == stationId.Value);
                }

                var data = await query
                    .OrderBy(x => x.StationId)
                    .ThenBy(x => x.SortOrder)
                    .ThenBy(x => x.PoleCode)
                    .Select(x => new
                    {
                        id = x.Id,
                        stationId = x.StationId,
                        stationName = x.ChargingStation != null ? x.ChargingStation.Name : string.Empty,
                        poleCode = x.PoleCode,
                        chargerType = x.ChargerType,
                        maxPower = x.MaxPower,
                        status = ChargingStatus.NormalizeNodeStatus(x.Status),
                        note = x.Note,
                        sortOrder = x.SortOrder,
                        ownerUserId = x.ChargingStation != null ? x.ChargingStation.OwnerUserId : null,
                        isAdminManaged = x.ChargingStation != null && !x.ChargingStation.OwnerUserId.HasValue
                    })
                    .ToListAsync();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi tải danh sách trụ sạc: {GetInnermostMessage(ex)}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPoleById(int id)
        {
            try
            {
                var pole = await _context.ChargingPoles
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new
                    {
                        id = x.Id,
                        stationId = x.StationId,
                        poleCode = x.PoleCode,
                        chargerType = x.ChargerType,
                        maxPower = x.MaxPower,
                        status = ChargingStatus.NormalizeNodeStatus(x.Status),
                        note = x.Note,
                        sortOrder = x.SortOrder,
                        ownerUserId = x.ChargingStation != null ? x.ChargingStation.OwnerUserId : null,
                        isAdminManaged = x.ChargingStation != null && !x.ChargingStation.OwnerUserId.HasValue
                    })
                    .FirstOrDefaultAsync();

                if (pole == null)
                    return NotFound(new { message = "Không tìm thấy trụ sạc." });

                return Ok(pole);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi tải chi tiết trụ sạc: {GetInnermostMessage(ex)}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePole([FromBody] CreateChargingPoleRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            try
            {
                var stationId = request.StationId;
                var poleCode = request.PoleCode?.Trim();
                if (stationId <= 0)
                    return BadRequest(new { message = "Vui lòng chọn trạm sạc." });

                if (string.IsNullOrWhiteSpace(poleCode))
                    return BadRequest(new { message = "Vui lòng nhập mã trụ." });

                var normalizedChargerType = ChargerTypeCatalog.Normalize(request.ChargerType);
                if (string.IsNullOrWhiteSpace(normalizedChargerType))
                    return BadRequest(new { message = "Vui lòng chọn loại sạc hợp lệ." });

                var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == stationId);
                if (station == null)
                    return BadRequest(new { message = "Trạm sạc không tồn tại." });

                if (station.OwnerUserId.HasValue)
                    return BadRequest(new { message = "Chỉ được thêm trụ trực tiếp cho các trạm do admin tạo. Trạm do user gửi yêu cầu phải quản lý qua luồng duyệt riêng." });

                var duplicated = await _context.ChargingPoles.AnyAsync(x =>
                    x.StationId == stationId &&
                    x.PoleCode == poleCode);

                if (duplicated)
                    return BadRequest(new { message = "Mã trụ đã tồn tại trong trạm này." });

                var sortOrder = request.SortOrder;
                if (sortOrder <= 0)
                {
                    var lastSortOrder = await _context.ChargingPoles
                        .Where(x => x.StationId == stationId)
                        .Select(x => (int?)x.SortOrder)
                        .MaxAsync() ?? 0;

                    sortOrder = lastSortOrder + 1;
                }

                var normalizedPoleStatus = station.Status == ChargingStatus.Inactive
                    ? ChargingStatus.Inactive
                    : ChargingStatus.NormalizeNodeStatus(request.Status);

                var pole = new ChargingPole
                {
                    StationId = stationId,
                    PoleCode = poleCode,
                    ChargerType = normalizedChargerType,
                    MaxPower = ChargingStatus.NormalizeKw(request.MaxPower),
                    Status = normalizedPoleStatus,
                    SortOrder = sortOrder,
                    Note = request.Note?.Trim()
                };

                _context.ChargingPoles.Add(pole);
                await _context.SaveChangesAsync();
                await _hierarchyService.SyncStationFromChildrenAsync(pole.StationId);

                return Ok(new { message = "Thêm trụ sạc thành công." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = $"Lỗi lưu trụ sạc: {GetInnermostMessage(ex)}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi thêm trụ sạc: {GetInnermostMessage(ex)}" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePole(int id, [FromBody] UpdateChargingPoleRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });

            try
            {
                var pole = await _context.ChargingPoles
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (pole == null)
                    return NotFound(new { message = "Không tìm thấy trụ sạc." });

                var stationId = request.StationId;
                var poleCode = request.PoleCode?.Trim();

                if (stationId <= 0)
                    return BadRequest(new { message = "Vui lòng chọn trạm sạc." });

                if (string.IsNullOrWhiteSpace(poleCode))
                    return BadRequest(new { message = "Vui lòng nhập mã trụ." });

                var normalizedChargerType = ChargerTypeCatalog.Normalize(request.ChargerType);
                if (string.IsNullOrWhiteSpace(normalizedChargerType))
                    return BadRequest(new { message = "Vui lòng chọn loại sạc hợp lệ." });

                var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == stationId);
                if (station == null)
                    return BadRequest(new { message = "Trạm sạc không tồn tại." });

                if (stationId != pole.StationId && station.OwnerUserId.HasValue)
                    return BadRequest(new { message = "Không thể chuyển trụ sang trạm do user gửi yêu cầu." });

                var duplicated = await _context.ChargingPoles.AnyAsync(x =>
                    x.Id != id &&
                    x.StationId == stationId &&
                    x.PoleCode == poleCode);

                if (duplicated)
                    return BadRequest(new { message = "Mã trụ đã tồn tại trong trạm này." });

                var oldStationId = pole.StationId;

                pole.StationId = stationId;
                pole.PoleCode = poleCode;
                pole.ChargerType = normalizedChargerType;
                pole.MaxPower = ChargingStatus.NormalizeKw(request.MaxPower);
                pole.Status = station.Status == ChargingStatus.Inactive
                    ? ChargingStatus.Inactive
                    : ChargingStatus.NormalizeNodeStatus(request.Status);
                pole.SortOrder = request.SortOrder <= 0 ? pole.SortOrder : request.SortOrder;
                pole.Note = request.Note?.Trim();

                await _context.SaveChangesAsync();
                await _hierarchyService.SyncStationFromChildrenAsync(pole.StationId);

                if (oldStationId != pole.StationId)
                {
                    await _hierarchyService.SyncStationFromChildrenAsync(oldStationId);
                }

                return Ok(new { message = "Cập nhật trụ sạc thành công." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = $"Lỗi cập nhật trụ sạc: {GetInnermostMessage(ex)}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi cập nhật trụ sạc: {GetInnermostMessage(ex)}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePole(int id)
        {
            try
            {
                var pole = await _context.ChargingPoles.FirstOrDefaultAsync(x => x.Id == id);
                if (pole == null)
                    return NotFound(new { message = "Không tìm thấy trụ sạc." });

                var stationId = pole.StationId;

                _context.ChargingPoles.Remove(pole);
                await _context.SaveChangesAsync();
                await _hierarchyService.SyncStationFromChildrenAsync(stationId);

                return Ok(new { message = "Xóa trụ sạc thành công." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = $"Lỗi xóa trụ sạc: {GetInnermostMessage(ex)}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi xóa trụ sạc: {GetInnermostMessage(ex)}" });
            }
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
