using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Why changed: only admin can manage stations
    public class StationController : Controller
    {
        public IActionResult Index() => View();

        public IActionResult Details(int id)
        {
            ViewBag.StationId = id;
            return View();
        }

        public IActionResult Edit(int id)
        {
            ViewBag.StationId = id;
            return View();
        }

        public IActionResult Create() => View();
    }
}