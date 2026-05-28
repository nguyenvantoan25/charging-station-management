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
    public class StationController : Controller
    {
        private readonly AppDbContext _context;

        public StationController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Favorites()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new
                {
                    area = "User",
                    returnUrl = Url.Action("Favorites", "Station", new { area = "User" })
                });
            }

            var model = await _context.FavoriteStations
                .Where(x => x.UserId == currentUserId.Value &&
                            x.ChargingStation != null &&
                            x.ChargingStation.SystemStatus == StationSystemStatus.Approved &&
                            x.ChargingStation.IsVisible &&
                            (x.ChargingStation.MaintenanceFeeStatus == StationMaintenanceStatus.Paid ||
                             x.ChargingStation.MaintenanceFeeStatus == StationMaintenanceStatus.Active ||
                             x.ChargingStation.MaintenanceFeeStatus == StationMaintenanceStatus.ExpiringSoon) &&
                            (x.ChargingStation.OperationStatus == StationOperationStatus.Active ||
                             x.ChargingStation.Status == ChargingStatus.Active))
                .Include(x => x.ChargingStation)
                    .ThenInclude(x => x!.Reviews)
                .Include(x => x.ChargingStation)
                    .ThenInclude(x => x!.ChargingPoles)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new StationListItemViewModel
                {
                    Id = x.StationId,
                    Name = x.ChargingStation!.Name,
                    Address = x.ChargingStation.Address,
                    Status = StationOperationStatus.ToDisplay(x.ChargingStation.OperationStatus),
                    ChargerType = x.ChargingStation.ChargerType,
                    Power = x.ChargingStation.Power,
                    PricePerKwh = x.ChargingStation.PricePerKwh,
                    Latitude = x.ChargingStation.Latitude,
                    Longitude = x.ChargingStation.Longitude,
                    AverageRating = x.ChargingStation.Reviews.Any()
                        ? Math.Round(x.ChargingStation.Reviews.Average(r => (double)r.Rating), 1)
                        : 0,
                    ReviewCount = x.ChargingStation.Reviews.Count(),
                    TotalPoleCount = x.ChargingStation.ChargingPoles.Count(),
                    AvailablePoleCount = x.ChargingStation.ChargingPoles.Count(p => p.Status == ChargingStatus.Active),
                    IsFavorite = true
                })
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var station = await _context.ChargingStations
                .Include(x => x.Reviews)
                .Include(x => x.ChargingPoles)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (station == null)
                return NotFound();

            var currentUserId = GetCurrentUserId();
            var canViewAsOwner = currentUserId.HasValue && station.OwnerUserId == currentUserId.Value;
            if (!canViewAsOwner && !StationMaintenanceService.IsPubliclyUsable(station))
            {
                return NotFound();
            }

            var currentUserName = User.Identity?.Name;

            var isFavorite = false;
            StationReview? currentUserReview = null;

            if (currentUserId != null)
            {
                isFavorite = await _context.FavoriteStations
                    .AnyAsync(x => x.UserId == currentUserId.Value && x.StationId == id);
            }

            if (!string.IsNullOrWhiteSpace(currentUserName))
            {
                // Why changed: load the user's own review so the form can be prefilled for editing.
                currentUserReview = station.Reviews
                    .FirstOrDefault(x => x.UserName == currentUserName);
            }

            var model = new StationDetailsViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Address = station.Address,
                Status = StationOperationStatus.ToDisplay(station.OperationStatus),
                ChargerType = station.ChargerType,
                Power = station.Power,
                PricePerKwh = station.PricePerKwh,
                Latitude = station.Latitude,
                Longitude = station.Longitude,
                AverageRating = station.Reviews.Any() ? Math.Round(station.Reviews.Average(x => x.Rating), 1) : 0,
                ReviewCount = station.Reviews.Count,
                TotalPoleCount = station.ChargingPoles.Count,
                AvailablePoleCount = station.ChargingPoles.Count(x => x.Status == ChargingStatus.Active || x.Status == "Còn sẵn"),
                IsFavorite = isFavorite,

                // Why changed: allow edit mode instead of blocking user after first review.
                HasReviewed = currentUserReview != null,
                CurrentUserRating = currentUserReview?.Rating,
                CurrentUserComment = currentUserReview?.Comment,

                Poles = station.ChargingPoles
                    .OrderBy(x => x.PoleCode)
                    .Select(x => new PoleItemViewModel
                    {
                        Id = x.Id,
                        PoleCode = x.PoleCode,
                        MaxPower = x.MaxPower,
                        Status = ChargingStatus.NormalizeNodeStatus(x.Status),
                        Note = x.Note
                    })
                    .ToList(),

                Reviews = station.Reviews
                    .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                    .Select(x => new ReviewItemViewModel
                    {
                        Id = x.Id,
                        Rating = x.Rating,
                        Comment = x.Comment,
                        UserName = x.UserName,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReviewAjax([FromBody] CreateReviewViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ."
                });
            }

            var station = await _context.ChargingStations.FindAsync(model.StationId);
            if (station == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy trạm."
                });
            }

            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập."
                });
            }

            // Why changed: use upsert so one user can edit their existing review instead of creating duplicate rows.
            var existedReview = await _context.StationReviews
                .FirstOrDefaultAsync(x => x.StationId == model.StationId && x.UserName == currentUserName);

            string successMessage;

            if (existedReview == null)
            {
                var review = new StationReview
                {
                    StationId = model.StationId,
                    Rating = model.Rating,
                    Comment = model.Comment,
                    UserName = currentUserName,
                    CreatedAt = DateTime.Now
                };

                _context.StationReviews.Add(review);
                successMessage = "Gửi đánh giá thành công.";
            }
            else
            {
                existedReview.Rating = model.Rating;
                existedReview.Comment = model.Comment;
                existedReview.UpdatedAt = DateTime.Now; // Why changed: mark the review as edited for recent dashboard activity.
                successMessage = "Cập nhật đánh giá thành công.";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = successMessage
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
