using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.Admin.Models.Dto;
using tramsac99.Areas.Admin.ViewModels;
using tramsac99.Data;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var stations = await _context.ChargingStations
                .AsNoTracking()
                .Select(s => new ChargingStationDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Address = s.Address,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Status = s.Status,
                    ChargerType = s.ChargerType,
                    Power = s.Power,
                    PricePerKwh = s.PricePerKwh,
                    AverageRating = s.Reviews.Any() ? Math.Round(s.Reviews.Average(r => (double)r.Rating), 1) : 0,
                    ReviewCount = s.Reviews.Count(),
                    PoleCount = s.ChargingPoles.Count(),
                    ActivePoleCount = s.ChargingPoles.Count(p => p.Status == ChargingStatus.Active),
                    ActivePortCount = 0,
                    PortCount = 0,
                    OwnerUserId = s.OwnerUserId,
                    IsAdminManaged = !s.OwnerUserId.HasValue
                })
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TotalStations = stations.Count,
                AdminManagedStations = stations.Count(x => x.IsAdminManaged),
                UserSubmittedStations = stations.Count(x => !x.IsAdminManaged),
                TotalReviews = stations.Sum(x => x.ReviewCount),
                TotalPoles = stations.Sum(x => x.PoleCount),
                ActivePoles = stations.Sum(x => x.ActivePoleCount),
                Stations = stations
            };

            return View(model);
        }
    }
}
