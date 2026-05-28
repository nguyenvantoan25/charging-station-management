using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ChargingPortController : Controller
    {
        public IActionResult Index(int? stationId, int? poleId)
        {
            return RedirectToAction("Index", "ChargingPole", new { area = "Admin", stationId });
        }
    }
}
