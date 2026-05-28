using Microsoft.AspNetCore.Mvc;

namespace tramsac99.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home", new { area = "User" });
        }
    }
}