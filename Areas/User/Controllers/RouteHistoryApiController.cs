using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Data;

namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    [Route("User/api/routes")]
    [ApiController]
    public class RouteHistoryApiController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly AppDbContext _context;

        public RouteHistoryApiController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyRoutes([FromQuery] bool favoriteOnly = false, [FromQuery] int take = 80)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để xem lịch sử lộ trình." });
            }

            take = Math.Clamp(take, 1, 120);

            var query = _context.RouteHistories
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId.Value);

            if (favoriteOnly)
            {
                query = query.Where(x => x.IsFavorite);
            }

            var items = await query
                .OrderByDescending(x => x.IsFavorite)
                .ThenByDescending(x => x.LastUsedAt ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                items = items.Select(x => BuildRouteDto(x)).ToList()
            });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRoute([FromBody] SaveRouteHistoryRequest model)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để lưu lộ trình." });
            }

            var validationMessage = ValidateSaveRequest(model);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return BadRequest(new { success = false, message = validationMessage });
            }

            var now = DateTime.Now;
            RouteHistory? item = null;

            if (model.Id.HasValue)
            {
                item = await _context.RouteHistories
                    .FirstOrDefaultAsync(x => x.Id == model.Id.Value && x.UserId == currentUserId.Value);
            }

            if (item == null)
            {
                item = new RouteHistory
                {
                    UserId = currentUserId.Value,
                    CreatedAt = now
                };
                _context.RouteHistories.Add(item);
            }

            ApplyRequestToEntity(item, model, now);
            item.LastUsedAt = now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã lưu lộ trình vào lịch sử.",
                item = BuildRouteDto(item)
            });
        }

        [Authorize]
        [HttpPost("{id:int}/favorite")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập." });
            }

            var item = await _context.RouteHistories
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId.Value);

            if (item == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lộ trình." });
            }

            item.IsFavorite = !item.IsFavorite;
            item.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isFavorite = item.IsFavorite,
                message = item.IsFavorite ? "Đã đánh dấu lộ trình yêu thích." : "Đã bỏ yêu thích lộ trình.",
                item = BuildRouteDto(item)
            });
        }

        [Authorize]
        [HttpPost("{id:int}/share")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareRoute(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập." });
            }

            var item = await _context.RouteHistories
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId.Value);

            if (item == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lộ trình." });
            }

            if (string.IsNullOrWhiteSpace(item.ShareToken))
            {
                item.ShareToken = await GenerateUniqueShareTokenAsync();
            }

            item.IsShared = true;
            item.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var shareUrl = Url.Action(
                action: "Route",
                controller: "Home",
                values: new { area = "User", share = item.ShareToken },
                protocol: Request.Scheme);

            return Ok(new
            {
                success = true,
                message = "Đã tạo liên kết chia sẻ lộ trình.",
                shareUrl,
                item = BuildRouteDto(item)
            });
        }

        [Authorize]
        [HttpPost("{id:int}/used")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUsed(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập." });
            }

            var item = await _context.RouteHistories
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId.Value);

            if (item == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lộ trình." });
            }

            item.LastUsedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập." });
            }

            var item = await _context.RouteHistories
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId.Value);

            if (item == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lộ trình." });
            }

            _context.RouteHistories.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa lộ trình khỏi lịch sử." });
        }

        [AllowAnonymous]
        [HttpGet("shared/{token}")]
        public async Task<IActionResult> GetSharedRoute(string token)
        {
            token = (token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { success = false, message = "Liên kết chia sẻ không hợp lệ." });
            }

            var item = await _context.RouteHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShareToken == token && x.IsShared);

            if (item == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lộ trình chia sẻ hoặc liên kết đã bị tắt." });
            }

            return Ok(new
            {
                success = true,
                item = BuildRouteDto(item, includeOwnerOnlyFields: false)
            });
        }

        private static string? TrimTo(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static string? ValidateSaveRequest(SaveRouteHistoryRequest model)
        {
            if (model == null)
            {
                return "Dữ liệu lộ trình không hợp lệ.";
            }

            if (model.Start == null || model.End == null)
            {
                return "Lộ trình cần có điểm đi và điểm đến.";
            }

            if (!IsValidCoordinate(model.Start.Lat, model.Start.Lng) || !IsValidCoordinate(model.End.Lat, model.End.Lng))
            {
                return "Tọa độ điểm đi hoặc điểm đến không hợp lệ.";
            }

            if (!IsVietnamCoordinate(model.Start.Lat, model.Start.Lng) || !IsVietnamCoordinate(model.End.Lat, model.End.Lng))
            {
                return "Hệ thống chỉ cho phép lưu lộ trình có điểm đi và điểm đến thuộc Việt Nam.";
            }

            if (model.TotalDistanceKm <= 0)
            {
                return "Quãng đường lộ trình chưa hợp lệ.";
            }

            return null;
        }

        private static bool IsValidCoordinate(double lat, double lng)
        {
            return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
        }

        private static bool IsVietnamCoordinate(double lat, double lng)
        {
            return lat >= 8.0 && lat <= 23.8 && lng >= 102.0 && lng <= 110.5;
        }

        private static void ApplyRequestToEntity(RouteHistory item, SaveRouteHistoryRequest model, DateTime now)
        {
            var startName = TrimTo(model.Start?.DisplayName, 250) ?? "Điểm đi";
            var endName = TrimTo(model.End?.DisplayName, 250) ?? "Điểm đến";

            item.RouteName = TrimTo(model.RouteName, 200) ?? $"{startName} → {endName}";
            item.StartName = startName;
            item.StartAddress = TrimTo(model.Start?.Address, 500);
            item.StartLatitude = model.Start?.Lat ?? 0;
            item.StartLongitude = model.Start?.Lng ?? 0;
            item.EndName = endName;
            item.EndAddress = TrimTo(model.End?.Address, 500);
            item.EndLatitude = model.End?.Lat ?? 0;
            item.EndLongitude = model.End?.Lng ?? 0;
            item.TotalDistanceKm = Math.Round(Math.Max(0, model.TotalDistanceKm), 2);
            item.FirstLegKm = Math.Round(Math.Max(0, model.FirstLegKm), 2);
            item.VehicleRangeKm = Math.Max(0, model.VehicleRangeKm);
            item.StartBattery = Math.Clamp(model.StartBattery, 0, 100);
            item.ReserveBattery = Math.Clamp(model.ReserveBattery, 0, 100);
            item.MaxDetourKm = Math.Max(0, model.MaxDetourKm);
            item.StopCount = model.Stops?.Count ?? 0;
            item.StopsJson = JsonSerializer.Serialize(model.Stops ?? new List<RouteStopRequest>(), JsonOptions);
            item.RoutePathJson = JsonSerializer.Serialize(CompactRoutePath(model.RoutePath), JsonOptions);
            item.IsFavorite = model.IsFavorite ?? item.IsFavorite;
            item.UpdatedAt = now;
        }

        private static List<RoutePointRequest> CompactRoutePath(List<RoutePointRequest>? routePath)
        {
            var validPath = (routePath ?? new List<RoutePointRequest>())
                .Where(x => IsValidCoordinate(x.Lat, x.Lng))
                .ToList();

            if (validPath.Count <= 180)
            {
                return validPath;
            }

            var result = new List<RoutePointRequest>();
            var step = Math.Ceiling(validPath.Count / 180.0);

            for (var index = 0; index < validPath.Count; index += (int)step)
            {
                result.Add(validPath[index]);
            }

            var last = validPath[^1];
            if (result.Count == 0 || result[^1].Lat != last.Lat || result[^1].Lng != last.Lng)
            {
                result.Add(last);
            }

            return result;
        }

        private object BuildRouteDto(RouteHistory item, bool includeOwnerOnlyFields = true)
        {
            var shareUrl = !string.IsNullOrWhiteSpace(item.ShareToken)
                ? Url.Action(
                    action: "Route",
                    controller: "Home",
                    values: new { area = "User", share = item.ShareToken },
                    protocol: Request.Scheme)
                : null;

            return new
            {
                item.Id,
                item.RouteName,
                start = new
                {
                    displayName = item.StartName,
                    address = item.StartAddress,
                    lat = item.StartLatitude,
                    lng = item.StartLongitude
                },
                end = new
                {
                    displayName = item.EndName,
                    address = item.EndAddress,
                    lat = item.EndLatitude,
                    lng = item.EndLongitude
                },
                item.TotalDistanceKm,
                item.FirstLegKm,
                item.VehicleRangeKm,
                item.StartBattery,
                item.ReserveBattery,
                item.MaxDetourKm,
                item.StopCount,
                stops = DeserializeJson<List<RouteStopRequest>>(item.StopsJson) ?? new List<RouteStopRequest>(),
                routePath = DeserializeJson<List<RoutePointRequest>>(item.RoutePathJson) ?? new List<RoutePointRequest>(),
                item.IsFavorite,
                item.IsShared,
                shareUrl = includeOwnerOnlyFields ? shareUrl : null,
                item.CreatedAt,
                item.UpdatedAt,
                item.LastUsedAt
            };
        }

        private static T? DeserializeJson<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch
            {
                return default;
            }
        }

        private async Task<string> GenerateUniqueShareTokenAsync()
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                var exists = await _context.RouteHistories.AnyAsync(x => x.ShareToken == token);

                if (!exists)
                {
                    return token;
                }
            }

            return Guid.NewGuid().ToString("N");
        }

        private int? GetCurrentUserId()
        {
            var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(rawUserId, out var userId))
            {
                return userId;
            }

            return null;
        }
    }

    public class SaveRouteHistoryRequest
    {
        public int? Id { get; set; }
        public string? RouteName { get; set; }
        public RoutePlaceRequest? Start { get; set; }
        public RoutePlaceRequest? End { get; set; }
        public double TotalDistanceKm { get; set; }
        public double FirstLegKm { get; set; }
        public int VehicleRangeKm { get; set; }
        public int StartBattery { get; set; }
        public int ReserveBattery { get; set; }
        public int MaxDetourKm { get; set; }
        public bool? IsFavorite { get; set; }
        public List<RoutePointRequest>? RoutePath { get; set; }
        public List<RouteStopRequest>? Stops { get; set; }
    }

    public class RoutePlaceRequest
    {
        public string? DisplayName { get; set; }
        public string? Address { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class RoutePointRequest
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class RouteStopRequest
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double ProgressKm { get; set; }
        public double RouteDistanceKm { get; set; }
        public double DetourKm { get; set; }
        public int ActivePoleCount { get; set; }
        public int AvailablePoleCount { get; set; }
        public int TotalPoleCount { get; set; }
        public double AverageRating { get; set; }
        public string? Status { get; set; }
    }
}
