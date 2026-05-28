using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ChargingPoleController : Controller
    {
        public IActionResult Index(int? stationId)
        {
            ViewBag.StationId = stationId; // Why changed: open pole page filtered by station
            return View();
        }
    }
}