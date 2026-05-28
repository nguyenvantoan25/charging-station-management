using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.Admin.ViewModels;
using tramsac99.Data;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class StationRequestController : Controller
    {
        private readonly AppDbContext _context;

        public StationRequestController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int registrationPage = 1, int operationPage = 1, string tab = "registration")
        {
            const int pageSize = 8;

            var registrationQuery = _context.StationRegistrationRequests
                .Include(x => x.User)
                .OrderBy(x => x.ApprovalStatus == StationWorkflowStatus.Pending ? 0
                    : x.ApprovalStatus == StationWorkflowStatus.AwaitingPayment ? 1
                    : x.ApprovalStatus == StationWorkflowStatus.Approved ? 2
                    : x.ApprovalStatus == StationWorkflowStatus.Completed ? 3
                    : 4)
                .ThenByDescending(x => x.CreatedAt)
                .AsQueryable();

            var totalCount = await registrationQuery.CountAsync();
            var safePage = Math.Max(1, registrationPage);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            safePage = Math.Min(safePage, totalPages);

            var model = new StationRequestIndexViewModel
            {
                RegistrationItems = await registrationQuery
                    .Skip((safePage - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(),
                RegistrationPage = safePage,
                RegistrationPageSize = pageSize,
                RegistrationTotalCount = totalCount,
                PendingRegistrations = await _context.StationRegistrationRequests.CountAsync(x => x.ApprovalStatus == StationWorkflowStatus.Pending),
                AwaitingPaymentCount = await _context.StationRegistrationRequests.CountAsync(x => x.ApprovalStatus == StationWorkflowStatus.Approved || x.ApprovalStatus == StationWorkflowStatus.AwaitingPayment),
                ApprovedCount = await _context.StationRegistrationRequests.CountAsync(x => x.ApprovalStatus == StationWorkflowStatus.Completed || x.CreatedStationId.HasValue),
                RejectedCount = await _context.StationRegistrationRequests.CountAsync(x => x.ApprovalStatus == StationWorkflowStatus.Rejected)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistration(int id, int registrationPage = 1, int operationPage = 1, string tab = "registration")
        {
            var request = await _context.StationRegistrationRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                TempData["AdminStationRequestError"] = "Không tìm thấy yêu cầu đăng ký trạm.";
                return RedirectToAction(nameof(Index), new { registrationPage });
            }

            if (request.ApprovalStatus != StationWorkflowStatus.Pending)
            {
                TempData["AdminStationRequestError"] = "Yêu cầu đăng ký trạm này đã được xử lý trước đó.";
                return RedirectToAction(nameof(Index), new { registrationPage });
            }

            request.ApprovalStatus = StationWorkflowStatus.Approved;
            request.ReviewedAt = DateTime.Now;
            request.AdminNote = $"Đã duyệt. Chờ chủ trạm thanh toán phí đăng ký {request.FeeAmount:N0}đ.";
            await _context.SaveChangesAsync();

            TempData["AdminStationRequestSuccess"] = "Đã duyệt yêu cầu đăng ký trạm. User có thể thanh toán để kích hoạt trạm.";
            return RedirectToAction(nameof(Index), new { registrationPage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int id, string? adminNote, int registrationPage = 1, int operationPage = 1, string tab = "registration")
        {
            var request = await _context.StationRegistrationRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                TempData["AdminStationRequestError"] = "Không tìm thấy yêu cầu đăng ký trạm.";
                return RedirectToAction(nameof(Index), new { registrationPage });
            }

            if (request.CreatedStationId.HasValue)
            {
                TempData["AdminStationRequestError"] = "Yêu cầu này đã tạo trạm chính thức nên không thể từ chối.";
                return RedirectToAction(nameof(Index), new { registrationPage });
            }

            request.ApprovalStatus = StationWorkflowStatus.Rejected;
            request.ReviewedAt = DateTime.Now;
            request.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? "Yêu cầu chưa đạt điều kiện duyệt." : adminNote.Trim();
            await _context.SaveChangesAsync();

            TempData["AdminStationRequestSuccess"] = "Đã từ chối yêu cầu đăng ký trạm và lưu lý do cho user xem.";
            return RedirectToAction(nameof(Index), new { registrationPage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveOperation(int id, int registrationPage = 1, int operationPage = 1, string tab = "registration")
        {
            TempData["AdminStationRequestError"] = "Theo nghiệp vụ mới, admin không duyệt thêm/sửa/xóa trụ hoặc đổi trạng thái vận hành. Chủ trạm tự thao tác, admin chỉ xem lịch sử.";
            return RedirectToAction(nameof(Index), new { registrationPage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectOperation(int id, string? adminNote, int registrationPage = 1, int operationPage = 1, string tab = "registration")
        {
            TempData["AdminStationRequestError"] = "Theo nghiệp vụ mới, không còn luồng từ chối yêu cầu vận hành. Admin chỉ duyệt đăng ký trạm mới.";
            return RedirectToAction(nameof(Index), new { registrationPage });
        }
    }
}
