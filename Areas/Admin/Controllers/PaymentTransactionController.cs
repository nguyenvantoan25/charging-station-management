using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Data;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PaymentTransactionController : Controller
    {
        private readonly AppDbContext _context;

        public PaymentTransactionController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string type = "all", string status = "all", string keyword = "", int page = 1, int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);
            keyword = (keyword ?? string.Empty).Trim().ToLowerInvariant();
            type = (type ?? "all").Trim();
            status = (status ?? "all").Trim();

            var query = _context.PaymentTransactions
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Station)
                .Include(x => x.RegistrationRequest)
                .AsQueryable();

            if (!string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (type == PaymentTransactionType.InitialRegistration)
                {
                    var initialTypes = new[] { PaymentTransactionType.InitialRegistration, PaymentTransactionType.LegacyInitialRegistration };
                    query = query.Where(x => initialTypes.Contains(x.PaymentType));
                }
                else if (type == PaymentTransactionType.Maintenance)
                {
                    var maintenanceTypes = new[] { PaymentTransactionType.Maintenance, PaymentTransactionType.LegacyMaintenance };
                    query = query.Where(x => maintenanceTypes.Contains(x.PaymentType));
                }
                else
                {
                    query = query.Where(x => x.PaymentType == type);
                }
            }

            if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    (x.User != null && ((x.User.Username ?? "").ToLower().Contains(keyword) || (x.User.Email ?? "").ToLower().Contains(keyword))) ||
                    (x.Station != null && ((x.Station.Name ?? "").ToLower().Contains(keyword) || (x.Station.Address ?? "").ToLower().Contains(keyword))) ||
                    (x.RegistrationRequest != null && (x.RegistrationRequest.StationName ?? "").ToLower().Contains(keyword)) ||
                    (x.Description ?? "").ToLower().Contains(keyword));
            }

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);

            var items = await query
                .OrderByDescending(x => x.PaidAt ?? x.CancelledAt ?? x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.Keyword = keyword;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = totalPages;

            return View(items);
        }
    }
}
