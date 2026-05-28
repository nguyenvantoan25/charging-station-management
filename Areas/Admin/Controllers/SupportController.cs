using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Data;
using tramsac99.Services;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SupportController : Controller
    {
        private readonly AppDbContext _context;
        private readonly LightAiService _lightAiService;

        public SupportController(AppDbContext context, LightAiService lightAiService)
        {
            _context = context;
            _lightAiService = lightAiService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ListData()
        {
            var data = await _context.SupportRequests
                .OrderByDescending(x => x.LastStatusChangedAt ?? x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.SenderUserId,
                    x.SenderUserName,
                    x.FullName,
                    x.Email,
                    x.PhoneNumber,
                    x.Subject,
                    x.Message,
                    x.Status,
                    x.IsRead,
                    x.CreatedAt,
                    x.ReadAt,
                    x.ResolvedAt,
                    x.AdminReply,
                    x.LastStatusChangedAt,
                    x.IsUserSeen,
                    x.UserSeenAt
                })
                .ToListAsync();

            var result = data.Select(x =>
            {
                var ticketAi = _lightAiService.ClassifySupportTicket(x.Subject, x.Message);
                return new
                {
                    x.Id,
                    x.SenderUserId,
                    x.SenderUserName,
                    x.FullName,
                    x.Email,
                    x.PhoneNumber,
                    x.Subject,
                    x.Message,
                    x.Status,
                    x.IsRead,
                    x.CreatedAt,
                    x.ReadAt,
                    x.ResolvedAt,
                    x.AdminReply,
                    x.LastStatusChangedAt,
                    x.IsUserSeen,
                    x.UserSeenAt,
                    category = ticketAi.Category,
                    priority = ticketAi.Priority,
                    aiSummary = ticketAi.Summary
                };
            }).ToList();

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> DetailsData(int id)
        {
            var item = await _context.SupportRequests
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.SenderUserId,
                    x.SenderUserName,
                    x.FullName,
                    x.Email,
                    x.PhoneNumber,
                    x.Subject,
                    x.Message,
                    x.Status,
                    x.IsRead,
                    x.CreatedAt,
                    x.ReadAt,
                    x.ResolvedAt,
                    x.AdminReply,
                    x.LastStatusChangedAt,
                    x.IsUserSeen,
                    x.UserSeenAt
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy yêu cầu hỗ trợ."
                });
            }

            var ticketAi = _lightAiService.ClassifySupportTicket(item.Subject, item.Message);

            return Json(new
            {
                success = true,
                item = new
                {
                    item.Id,
                    item.SenderUserId,
                    item.SenderUserName,
                    item.FullName,
                    item.Email,
                    item.PhoneNumber,
                    item.Subject,
                    item.Message,
                    item.Status,
                    item.IsRead,
                    item.CreatedAt,
                    item.ReadAt,
                    item.ResolvedAt,
                    item.AdminReply,
                    item.LastStatusChangedAt,
                    item.IsUserSeen,
                    item.UserSeenAt,
                    category = ticketAi.Category,
                    priority = ticketAi.Priority,
                    aiSummary = ticketAi.Summary
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var item = await _context.SupportRequests.FindAsync(id);
            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy yêu cầu hỗ trợ."
                });
            }

            item.IsRead = true;
            item.ReadAt ??= DateTime.Now;
            item.LastStatusChangedAt = DateTime.Now;

            if (string.IsNullOrWhiteSpace(item.Status) || item.Status == "Mới")
            {
                item.Status = "Đã đọc";
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã đánh dấu là đã đọc."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkResolved(int id, [FromForm] string? adminReply)
        {
            var item = await _context.SupportRequests.FindAsync(id);
            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy yêu cầu hỗ trợ."
                });
            }

            item.IsRead = true;
            item.ReadAt ??= DateTime.Now;
            item.Status = "Đã xử lý";
            item.ResolvedAt = DateTime.Now;
            item.AdminReply = string.IsNullOrWhiteSpace(adminReply) ? null : adminReply.Trim();
            item.LastStatusChangedAt = DateTime.Now;
            item.IsUserSeen = false;
            item.UserSeenAt = null;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã chuyển yêu cầu sang trạng thái xử lý xong và gửi thông báo trong app cho user."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.SupportRequests.FindAsync(id);
            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy yêu cầu hỗ trợ."
                });
            }

            _context.SupportRequests.Remove(item);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã xóa yêu cầu hỗ trợ."
            });
        }
    }
}
