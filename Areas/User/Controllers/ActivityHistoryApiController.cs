using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Data;

namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    [Route("User/api/activity-history")]
    [ApiController]
    public class ActivityHistoryApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ActivityHistoryApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string type = "all", [FromQuery] string keyword = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 8)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để xem lịch sử." });
            }

            var userName = User.Identity?.Name ?? string.Empty;
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 30);
            keyword = (keyword ?? string.Empty).Trim().ToLowerInvariant();
            type = (type ?? "all").Trim().ToLowerInvariant();

            var items = new List<ActivityHistoryItemDto>();

            var reviews = await _context.StationReviews
                .AsNoTracking()
                .Include(x => x.ChargingStation)
                .Where(x => x.UserName == userName)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Take(200)
                .ToListAsync();

            items.AddRange(reviews.Select(x => new ActivityHistoryItemDto
            {
                Type = "review",
                TypeText = "Đánh giá",
                Icon = "fa-star",
                Title = $"Đánh giá {x.Rating}/5 cho {x.ChargingStation?.Name ?? "trạm sạc"}",
                Description = string.IsNullOrWhiteSpace(x.Comment) ? "Không có nội dung đánh giá." : x.Comment!,
                AmountText = "",
                StatusText = x.UpdatedAt.HasValue ? "Đã chỉnh sửa" : "Đã gửi",
                ActivityAt = x.UpdatedAt ?? x.CreatedAt,
                Url = Url.Action("Details", "Station", new { area = "User", id = x.StationId }) ?? "#"
            }));

            var routes = await _context.RouteHistories
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.LastUsedAt ?? x.UpdatedAt)
                .Take(200)
                .ToListAsync();

            items.AddRange(routes.Select(x => new ActivityHistoryItemDto
            {
                Type = "route",
                TypeText = "Lộ trình",
                Icon = "fa-route",
                Title = x.RouteName,
                Description = $"{x.StartName} → {x.EndName} • {x.TotalDistanceKm:N0} km • {x.StopCount} điểm dừng",
                AmountText = "",
                StatusText = x.IsFavorite ? "Yêu thích" : (x.IsShared ? "Đã chia sẻ" : "Đã lưu"),
                ActivityAt = x.LastUsedAt ?? x.UpdatedAt,
                Url = Url.Action("Route", "Home", new { area = "User", reuseId = x.Id }) ?? "#"
            }));

            var transactions = await _context.PaymentTransactions
                .AsNoTracking()
                .Include(x => x.Station)
                .Include(x => x.RegistrationRequest)
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.PaidAt ?? x.CancelledAt ?? x.CreatedAt)
                .Take(200)
                .ToListAsync();

            items.AddRange(transactions.Select(x => new ActivityHistoryItemDto
            {
                Type = "transaction",
                TypeText = "Giao dịch",
                Icon = "fa-credit-card",
                Title = x.PaymentType,
                Description = x.Description ?? x.Station?.Name ?? x.RegistrationRequest?.StationName ?? "Giao dịch thanh toán",
                AmountText = x.Amount.ToString("N0") + "đ",
                StatusText = x.Status,
                ActivityAt = x.PaidAt ?? x.CancelledAt ?? x.CreatedAt,
                Url = x.StationId.HasValue
                    ? (Url.Action("Details", "StationRegistration", new { area = "User", id = x.StationId }) ?? "#")
                    : (Url.Action("MyStations", "StationRegistration", new { area = "User" }) ?? "#")
            }));

            if (type != "all")
            {
                items = items.Where(x => x.Type == type).ToList();
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                items = items.Where(x => ($"{x.Title} {x.Description} {x.StatusText} {x.AmountText}").ToLowerInvariant().Contains(keyword)).ToList();
            }

            var ordered = items.OrderByDescending(x => x.ActivityAt).ToList();
            var total = ordered.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);

            var summary = new
            {
                total = items.Count,
                reviews = items.Count(x => x.Type == "review"),
                routes = items.Count(x => x.Type == "route"),
                transactions = items.Count(x => x.Type == "transaction"),
                paidTransactions = items.Count(x => x.Type == "transaction" && x.StatusText == PaymentTransactionStatus.Paid)
            };

            return Ok(new
            {
                success = true,
                page,
                pageSize,
                total,
                totalPages,
                summary,
                items = ordered.Skip((page - 1) * pageSize).Take(pageSize)
            });
        }

        private int? GetCurrentUserId()
        {
            var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(rawUserId, out var userId) ? userId : null;
        }
    }

    public class ActivityHistoryItemDto
    {
        public string Type { get; set; } = "";
        public string TypeText { get; set; } = "";
        public string Icon { get; set; } = "fa-clock";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string AmountText { get; set; } = "";
        public string StatusText { get; set; } = "";
        public DateTime ActivityAt { get; set; }
        public string Url { get; set; } = "#";
    }
}
