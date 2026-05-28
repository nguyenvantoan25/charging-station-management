using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.User.ViewModels;
using tramsac99.Data;
using tramsac99.Services;

namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    [Route("User/api/home")]
    [ApiController]
    public class HomeApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ExternalEvNewsService _externalEvNewsService;
        private readonly LightAiService _lightAiService;

        public HomeApiController(
            AppDbContext context,
            ExternalEvNewsService externalEvNewsService,
            LightAiService lightAiService)
        {
            _context = context;
            _externalEvNewsService = externalEvNewsService;
            _lightAiService = lightAiService;
        }

        [HttpGet("live-news")]
        public async Task<IActionResult> GetLiveNews()
        {
            // Why changed: keep old endpoint working if another page still calls it.
            var totalStations = await _context.ChargingStations.CountAsync();
            var activeStations = await _context.ChargingStations.CountAsync(x =>
                x.SystemStatus == StationSystemStatus.Approved &&
                x.OperationStatus == StationOperationStatus.Active &&
                x.IsVisible &&
                (x.MaintenanceFeeStatus == StationMaintenanceStatus.Paid || x.MaintenanceFeeStatus == StationMaintenanceStatus.Active || x.MaintenanceFeeStatus == StationMaintenanceStatus.ExpiringSoon));
            var totalReviews = await _context.StationReviews.CountAsync();

            var latestReviews = await _context.StationReviews
                .Include(x => x.ChargingStation)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Take(8)
                .Select(x => new
                {
                    x.Id,
                    stationId = x.StationId,
                    stationName = x.ChargingStation != null ? x.ChargingStation.Name : "",
                    address = x.ChargingStation != null ? x.ChargingStation.Address : "",
                    x.UserName,
                    x.Rating,
                    x.Comment,
                    x.CreatedAt,
                    x.UpdatedAt,
                    activityAt = x.UpdatedAt ?? x.CreatedAt,
                    isEdited = x.UpdatedAt != null
                })
                .ToListAsync();

            var featuredStations = await _context.ChargingStations
                .Where(x => x.SystemStatus == StationSystemStatus.Approved &&
                            x.IsVisible &&
                            (x.MaintenanceFeeStatus == StationMaintenanceStatus.Paid ||
                             x.MaintenanceFeeStatus == StationMaintenanceStatus.Active ||
                             x.MaintenanceFeeStatus == StationMaintenanceStatus.ExpiringSoon) &&
                            (x.OperationStatus == StationOperationStatus.Active || x.Status == ChargingStatus.Active || x.Status == "Hoạt động"))
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Address,
                    Status = StationOperationStatus.ToDisplay(x.OperationStatus),
                    reviewCount = x.Reviews.Count(),
                    averageRating = x.Reviews.Any() ? Math.Round(x.Reviews.Average(r => (double)r.Rating), 1) : 0
                })
                .OrderByDescending(x => x.reviewCount)
                .ThenByDescending(x => x.averageRating)
                .Take(6)
                .ToListAsync();

            return Ok(new
            {
                serverTime = DateTime.Now,
                totalStations,
                activeStations,
                totalReviews,
                latestReviews,
                featuredStations
            });
        }

        [HttpGet("external-ev-news")]
        public async Task<IActionResult> GetExternalEvNews()
        {
            var items = await _externalEvNewsService.GetLatestAsync(60);

            return Ok(new
            {
                serverTime = DateTime.Now,
                sourceCount = items.Select(x => x.Source).Distinct().Count(),
                itemCount = items.Count,
                items
            });
        }

        [Authorize]
        [HttpPost("support-contact")]
        public async Task<IActionResult> CreateSupportContact([FromBody] ContactSupportRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(x => x.Errors)
                    .Select(x => x.ErrorMessage)
                    .FirstOrDefault();

                return BadRequest(new
                {
                    success = false,
                    message = firstError ?? "Dữ liệu liên hệ không hợp lệ."
                });
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập để gửi yêu cầu hỗ trợ."
                });
            }

            var currentUserName = User.Identity?.Name;
            var ticketAi = _lightAiService.ClassifySupportTicket(model.Subject, model.Message);

            var item = new SupportRequest
            {
                SenderUserId = currentUserId,
                SenderUserName = currentUserName,
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Subject = model.Subject.Trim(),
                Message = model.Message.Trim(),
                Status = "Mới",
                IsRead = false,
                CreatedAt = DateTime.Now,
                LastStatusChangedAt = DateTime.Now,
                IsUserSeen = true
            };

            _context.SupportRequests.Add(item);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã gửi yêu cầu hỗ trợ. Khi admin xử lý xong, mục Liên hệ sẽ hiện badge thông báo và ticket của bạn sẽ đổi trạng thái.",
                ai = new
                {
                    ticketAi.Category,
                    ticketAi.Priority,
                    ticketAi.Summary
                }
            });
        }

        [Authorize]
        [HttpGet("my-support-tickets")]
        public async Task<IActionResult> GetMySupportTickets()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập để xem yêu cầu hỗ trợ của mình."
                });
            }

            var items = await _context.SupportRequests
                .Where(x => x.SenderUserId == currentUserId.Value)
                .OrderByDescending(x => x.LastStatusChangedAt ?? x.CreatedAt)
                .Select(x => new SupportTicketItemViewModel
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Email = x.Email,
                    PhoneNumber = x.PhoneNumber,
                    Subject = x.Subject,
                    Message = x.Message,
                    Status = string.IsNullOrWhiteSpace(x.Status) ? "Mới" : x.Status,
                    IsRead = x.IsRead,
                    CreatedAt = x.CreatedAt,
                    ReadAt = x.ReadAt,
                    ResolvedAt = x.ResolvedAt,
                    AdminReply = x.AdminReply,
                    LastStatusChangedAt = x.LastStatusChangedAt,
                    IsUserSeen = x.IsUserSeen,
                    UserSeenAt = x.UserSeenAt
                })
                .ToListAsync();

            foreach (var item in items)
            {
                var ticketAi = _lightAiService.ClassifySupportTicket(item.Subject, item.Message);
                item.Category = ticketAi.Category;
                item.Priority = ticketAi.Priority;
                item.AiSummary = ticketAi.Summary;
            }

            return Ok(new
            {
                success = true,
                unresolvedCount = items.Count(x => x.Status != "Đã xử lý"),
                resolvedUnreadCount = items.Count(x => x.Status == "Đã xử lý" && !x.IsUserSeen),
                items
            });
        }

        [Authorize]
        [HttpGet("support-notification-count")]
        public async Task<IActionResult> GetSupportNotificationCount()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Ok(new
                {
                    success = true,
                    count = 0
                });
            }

            var count = await _context.SupportRequests
                .CountAsync(x => x.SenderUserId == currentUserId.Value
                                 && !x.IsUserSeen
                                 && (x.Status == "Đã xử lý" || !string.IsNullOrWhiteSpace(x.AdminReply)));

            return Ok(new
            {
                success = true,
                count
            });
        }

        [Authorize]
        [HttpPost("support-mark-seen/{id:int}")]
        public async Task<IActionResult> MarkSupportSeen(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập."
                });
            }

            var item = await _context.SupportRequests
                .FirstOrDefaultAsync(x => x.Id == id && x.SenderUserId == currentUserId.Value);

            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy yêu cầu hỗ trợ."
                });
            }

            item.IsUserSeen = true;
            item.UserSeenAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã xác nhận thông báo."
            });
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
}
