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
    [Authorize]
    public class StationRegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly PayOsCheckoutService _payOsCheckoutService;

        private const decimal InitialRegistrationFee = 50000m;
        private const decimal MonthlyMaintenanceFeeAmount = 10000m;
        public StationRegistrationController(
            AppDbContext context,
            IWebHostEnvironment environment,
            PayOsCheckoutService payOsCheckoutService)
        {
            _context = context;
            _environment = environment;
            _payOsCheckoutService = payOsCheckoutService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            var model = new RegisterStationRequestViewModel
            {
                ContactEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                OperatorName = User.Identity?.Name ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterStationRequestViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            if (string.IsNullOrWhiteSpace(model.Address))
            {
                ModelState.AddModelError(nameof(model.Address), "Vui lòng chọn địa chỉ từ bản đồ hoặc ô gợi ý.");
            }

            if (!ChargerTypeCatalog.IsValid(model.InitialPoleChargerType))
            {
                ModelState.AddModelError(nameof(model.InitialPoleChargerType), "Loại sạc không hợp lệ. Vui lòng chọn lại từ danh sách.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var imageUrl = await SaveRequestImageAsync(model.ImageFile);

            var request = new StationRegistrationRequest
            {
                UserId = user.Id,
                StationName = model.StationName.Trim(),
                OperatorName = model.OperatorName.Trim(),
                ContactEmail = model.ContactEmail.Trim(),
                ContactPhone = model.ContactPhone.Trim(),
                Address = model.Address.Trim(),
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Description = model.Description?.Trim(),
                ImageUrl = imageUrl,
                InitialPoleCount = model.InitialPoleCount,
                InitialPoleChargerType = ChargerTypeCatalog.Normalize(model.InitialPoleChargerType),
                InitialPoleMaxPower = ChargingStatus.NormalizeKw(model.InitialPoleMaxPower),
                InitialPoleNote = model.InitialPoleNote?.Trim(),
                ApprovalStatus = StationWorkflowStatus.Pending,
                PaymentStatus = "Chưa thanh toán",
                FeeAmount = InitialRegistrationFee,
                CreatedAt = DateTime.Now
            };

            _context.StationRegistrationRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["StationRequestSuccess"] = $"Đã gửi yêu cầu đăng ký trạm. Admin sẽ duyệt trước khi bạn thanh toán phí kích hoạt lần đầu {request.FeeAmount:N0}đ.";
            return RedirectToAction(nameof(MyStations));
        }

        [HttpGet]
        public async Task<IActionResult> MyStations()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var model = await BuildMyStationDashboardAsync(currentUserId.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyRegistrations()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var model = await BuildMyStationDashboardAsync(currentUserId.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyHistory()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var model = await BuildMyStationDashboardAsync(currentUserId.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            // Why changed: keep history and pole list paged on the server to reduce page height.
            var model = await BuildManageStationDetailsAsync(id, currentUserId.Value, historyPage, polePage);
            if (model == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm bạn cần xem chi tiết.";
                return RedirectToAction(nameof(MyStations));
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPayment(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var request = await _context.StationRegistrationRequests.FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId.Value);
            if (request == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy yêu cầu đăng ký trạm.";
                return RedirectToAction(nameof(MyStations));
            }

            if (request.ApprovalStatus != StationWorkflowStatus.Approved && request.ApprovalStatus != StationWorkflowStatus.AwaitingPayment)
            {
                TempData["StationRequestError"] = "Yêu cầu này chưa được admin duyệt để thanh toán.";
                return RedirectToAction(nameof(MyStations));
            }

            if (request.CreatedStationId.HasValue)
            {
                TempData["StationRequestSuccess"] = "Yêu cầu này đã hoàn tất.";
                return RedirectToAction(nameof(MyStations));
            }

            var returnUrl = Url.Action(nameof(PaymentReturn), "StationRegistration", new { area = "User", requestId = request.Id }, Request.Scheme) ?? string.Empty;
            var cancelUrl = Url.Action(nameof(PaymentReturn), "StationRegistration", new { area = "User", requestId = request.Id }, Request.Scheme) ?? string.Empty;
            var fallbackUrl = Url.Action(nameof(DemoComplete), "StationRegistration", new { area = "User", id = request.Id }, Request.Scheme) ?? string.Empty;

            request.PayOsOrderCode ??= await GenerateUniquePayOsOrderCodeAsync();

            var initialPaymentTypes = new[] { PaymentTransactionType.InitialRegistration, PaymentTransactionType.LegacyInitialRegistration };
            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(x =>
                    x.RegistrationRequestId == request.Id &&
                    initialPaymentTypes.Contains(x.PaymentType) &&
                    x.Status != PaymentTransactionStatus.Paid);

            if (transaction == null)
            {
                transaction = new PaymentTransaction
                {
                    UserId = currentUserId.Value,
                    RegistrationRequestId = request.Id,
                    PaymentType = PaymentTransactionType.InitialRegistration,
                    Amount = InitialRegistrationFee,
                    Description = $"Thanh toán lần đầu đăng ký trạm {request.StationName}",
                    CreatedAt = DateTime.Now
                };
                _context.PaymentTransactions.Add(transaction);
            }

            transaction.Amount = InitialRegistrationFee;
            transaction.PayOsOrderCode = request.PayOsOrderCode;
            transaction.Status = PaymentTransactionStatus.Pending;

            var payment = await _payOsCheckoutService.CreateStationPaymentAsync(request, returnUrl, cancelUrl, fallbackUrl);
            request.PayOsOrderCode = payment.orderCode;
            request.PayOsCheckoutUrl = payment.checkoutUrl;
            request.ApprovalStatus = StationWorkflowStatus.AwaitingPayment;
            request.PaymentStatus = payment.isFallback ? "Chờ thanh toán demo" : "Đang chờ thanh toán";
            transaction.PayOsOrderCode = payment.orderCode;
            transaction.PayOsCheckoutUrl = payment.checkoutUrl;
            transaction.Status = payment.isFallback ? "Chờ thanh toán demo" : PaymentTransactionStatus.Pending;
            await _context.SaveChangesAsync();

            return Redirect(payment.checkoutUrl);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentReturn(int? requestId, long? orderCode, string? status, string? code, bool? cancel)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            StationRegistrationRequest? request = null;

            if (requestId.HasValue)
            {
                request = await _context.StationRegistrationRequests.FirstOrDefaultAsync(x => x.Id == requestId.Value && x.UserId == currentUserId.Value);
            }

            if (request == null && orderCode.HasValue)
            {
                request = await _context.StationRegistrationRequests.FirstOrDefaultAsync(x => x.PayOsOrderCode == orderCode.Value && x.UserId == currentUserId.Value);
            }

            if (request == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy giao dịch thanh toán.";
                return RedirectToAction(nameof(MyStations));
            }

            var normalizedStatus = (status ?? string.Empty).Trim().ToUpperInvariant();
            var isPaid = normalizedStatus == "PAID" && cancel != true && string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);
            var isCancelled = cancel == true || normalizedStatus == "CANCELLED";

            var transaction = await FindInitialRegistrationTransactionAsync(request, orderCode);

            if (isPaid)
            {
                await CompletePaidStationRequestAsync(request, transaction);
                TempData["StationRequestSuccess"] = "Thanh toán thành công. Trạm đã được thêm tự động vào mục Trạm của tôi.";
            }
            else if (isCancelled)
            {
                request.PaymentStatus = "Đã hủy thanh toán";
                request.ApprovalStatus = StationWorkflowStatus.AwaitingPayment;
                if (transaction != null)
                {
                    transaction.Status = PaymentTransactionStatus.Cancelled;
                    transaction.CancelledAt = DateTime.Now;
                    transaction.Note = "Người dùng hủy giao dịch PayOS.";
                }
                await _context.SaveChangesAsync();
                TempData["StationRequestError"] = "Bạn đã hủy thanh toán PayOS. Có thể thanh toán lại bất kỳ lúc nào.";
            }
            else
            {
                if (transaction != null)
                {
                    transaction.Status = PaymentTransactionStatus.Failed;
                    transaction.Note = "PayOS chưa trả về trạng thái thanh toán thành công.";
                }
                await _context.SaveChangesAsync();
                TempData["StationRequestError"] = "Chưa ghi nhận thanh toán thành công. Vui lòng kiểm tra lại giao dịch.";
            }

            return RedirectToAction(nameof(MyStations));
        }

        [HttpGet]
        public async Task<IActionResult> DemoComplete(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var request = await _context.StationRegistrationRequests.FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUserId.Value);
            if (request == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy yêu cầu cần thanh toán demo.";
                return RedirectToAction(nameof(MyStations));
            }

            var transaction = await FindInitialRegistrationTransactionAsync(request, request.PayOsOrderCode);
            await CompletePaidStationRequestAsync(request, transaction);
            TempData["StationRequestSuccess"] = "Đã chạy luồng thanh toán demo. Trạm đã được thêm vào danh sách của bạn.";
            return RedirectToAction(nameof(MyStations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStatusRequest(StationStatusRequestViewModel model, bool returnToDetails = false, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var redirectAction = ResolveStationRedirect(returnToDetails, model.StationId, historyPage, polePage);

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == model.StationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Bạn không có quyền cập nhật trạm này.";
                return redirectAction;
            }

            if (!ModelState.IsValid)
            {
                TempData["StationRequestError"] = "Dữ liệu cập nhật trạng thái chưa hợp lệ.";
                return redirectAction;
            }

            var requestedStatus = (model.RequestedStatus ?? string.Empty).Trim();
            if (!IsAllowedOperationStatusInput(requestedStatus))
            {
                TempData["StationRequestError"] = "Trạng thái không hợp lệ.";
                return redirectAction;
            }

            if (!CanOwnerManageStation(station))
            {
                TempData["StationRequestError"] = "Trạm chưa được duyệt/kích hoạt hoặc đang bị admin khóa nên không thể cập nhật.";
                return redirectAction;
            }

            var oldOperationStatus = station.OperationStatus;
            var requestedOperationStatus = StationOperationStatus.Normalize(requestedStatus);
            station.OperationStatus = requestedOperationStatus;
            station.Status = StationOperationStatus.ToDisplay(requestedOperationStatus);

            if (requestedOperationStatus == StationOperationStatus.Inactive)
            {
                var poles = await _context.ChargingPoles
                    .Where(x => x.StationId == station.Id)
                    .ToListAsync();

                foreach (var pole in poles)
                {
                    pole.Status = ChargingStatus.Inactive;
                }
            }

            _context.StationOperationRequests.Add(new StationOperationRequest
            {
                StationId = station.Id,
                UserId = currentUserId.Value,
                RequestType = StationOperationRequestType.StatusUpdate,
                RequestedStationStatus = StationOperationStatus.ToDisplay(station.OperationStatus),
                UserNote = model.Note?.Trim(),
                Status = StationWorkflowStatus.Completed,
                AdminNote = "Chủ trạm tự cập nhật trực tiếp, không cần admin duyệt.",
                CreatedAt = DateTime.Now,
                ReviewedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            });

            LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.UpdateOperationStatus, oldOperationStatus, requestedOperationStatus, model.Note?.Trim());

            await _context.SaveChangesAsync();
            TempData["StationRequestSuccess"] = "Đã cập nhật trạng thái vận hành trạm trực tiếp.";
            return redirectAction;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAddPoleRequest(AddPoleRequestViewModel model, bool returnToDetails = false, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var redirectAction = ResolveStationRedirect(returnToDetails, model.StationId, historyPage, polePage);

            var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == model.StationId && x.OwnerUserId == currentUserId.Value);
            if (station == null)
            {
                TempData["StationRequestError"] = "Bạn không có quyền thêm trụ cho trạm này.";
                return redirectAction;
            }

            if (!CanOwnerManageStation(station))
            {
                TempData["StationRequestError"] = "Trạm chưa được duyệt/kích hoạt hoặc đang bị admin khóa nên không thể thêm trụ.";
                return redirectAction;
            }

            if (!ModelState.IsValid)
            {
                TempData["StationRequestError"] = "Dữ liệu thêm trụ chưa hợp lệ.";
                return redirectAction;
            }

            var normalizedCode = (model.PoleCode ?? string.Empty).Trim();
            var duplicatedPole = await _context.ChargingPoles
                .AnyAsync(x => x.StationId == station.Id && x.PoleCode == normalizedCode);

            if (duplicatedPole)
            {
                TempData["StationRequestError"] = $"Mã trụ {normalizedCode} đã tồn tại ở trạm này.";
                return redirectAction;
            }

            var nextSortOrder = await _context.ChargingPoles
                .Where(x => x.StationId == station.Id)
                .Select(x => (int?)x.SortOrder)
                .MaxAsync() ?? 0;

            var newPole = new ChargingPole
            {
                StationId = station.Id,
                PoleCode = normalizedCode,
                ChargerType = station.ChargerType,
                MaxPower = ChargingStatus.NormalizeKw(model.MaxPower),
                Status = ChargingStatus.Active,
                Note = model.Note?.Trim(),
                SortOrder = nextSortOrder + 1
            };

            _context.ChargingPoles.Add(newPole);

            _context.StationOperationRequests.Add(new StationOperationRequest
            {
                StationId = station.Id,
                UserId = currentUserId.Value,
                RequestType = StationOperationRequestType.AddPole,
                PoleCode = normalizedCode,
                PoleMaxPower = ChargingStatus.NormalizeKw(model.MaxPower),
                UserNote = model.Note?.Trim(),
                Status = StationWorkflowStatus.Completed,
                AdminNote = "Chủ trạm tự thêm trụ trực tiếp, không cần admin duyệt.",
                CreatedAt = DateTime.Now,
                ReviewedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            });

            LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.AddChargingPole, null, normalizedCode, $"Thêm trụ {normalizedCode} vào trạm.");

            await _context.SaveChangesAsync();
            TempData["StationRequestSuccess"] = "Đã thêm trụ mới trực tiếp vào trạm.";
            return redirectAction;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAddPoleBatchRequest(BulkAddPoleRequestViewModel model)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == model.StationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Bạn không có quyền thêm trụ cho trạm này.";
                return RedirectToAction(nameof(MyStations));
            }

            if (!CanOwnerManageStation(station))
            {
                TempData["StationRequestError"] = "Trạm chưa được duyệt/kích hoạt hoặc đang bị admin khóa nên không thể thêm trụ.";
                return RedirectToAction(nameof(MyStations));
            }

            if (model.Poles == null || !model.Poles.Any())
            {
                TempData["StationRequestError"] = "Vui lòng thêm ít nhất 1 trụ.";
                return RedirectToAction(nameof(MyStations));
            }

            var validPoles = model.Poles
                .Where(x => !string.IsNullOrWhiteSpace(x.PoleCode))
                .Select(x => new
                {
                    PoleCode = x.PoleCode!.Trim(),
                    MaxPower = ChargingStatus.NormalizeKw(x.MaxPower),
                    Note = x.Note?.Trim()
                })
                .ToList();

            if (!validPoles.Any())
            {
                TempData["StationRequestError"] = "Danh sách trụ chưa hợp lệ. Mỗi trụ cần có mã trụ.";
                return RedirectToAction(nameof(MyStations));
            }

            var duplicatedCodesInForm = validPoles
                .GroupBy(x => x.PoleCode, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatedCodesInForm.Any())
            {
                TempData["StationRequestError"] = $"Mã trụ bị trùng trong danh sách: {string.Join(", ", duplicatedCodesInForm)}.";
                return RedirectToAction(nameof(MyStations));
            }

            var existingCodes = await _context.ChargingPoles
                .Where(x => x.StationId == station.Id)
                .Select(x => x.PoleCode)
                .ToListAsync();

            var duplicatedCodesInDb = validPoles
                .Where(x => existingCodes.Any(code => string.Equals(code, x.PoleCode, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.PoleCode)
                .ToList();

            if (duplicatedCodesInDb.Any())
            {
                TempData["StationRequestError"] = $"Các mã trụ đã tồn tại: {string.Join(", ", duplicatedCodesInDb)}.";
                return RedirectToAction(nameof(MyStations));
            }

            var nextSortOrder = await _context.ChargingPoles
                .Where(x => x.StationId == station.Id)
                .Select(x => (int?)x.SortOrder)
                .MaxAsync() ?? 0;

            foreach (var pole in validPoles)
            {
                nextSortOrder++;

                _context.ChargingPoles.Add(new ChargingPole
                {
                    StationId = station.Id,
                    PoleCode = pole.PoleCode,
                    ChargerType = station.ChargerType,
                    MaxPower = pole.MaxPower,
                    Status = ChargingStatus.Active,
                    Note = pole.Note,
                    SortOrder = nextSortOrder
                });

                _context.StationOperationRequests.Add(new StationOperationRequest
                {
                    StationId = station.Id,
                    UserId = currentUserId.Value,
                    RequestType = StationOperationRequestType.AddPole,
                    PoleCode = pole.PoleCode,
                    PoleMaxPower = pole.MaxPower,
                    UserNote = pole.Note,
                    Status = StationWorkflowStatus.Completed,
                    AdminNote = "Chủ trạm tự thêm trụ trực tiếp, không cần admin duyệt.",
                    CreatedAt = DateTime.Now,
                    ReviewedAt = DateTime.Now,
                    CompletedAt = DateTime.Now
                });

                LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.AddChargingPole, null, pole.PoleCode, $"Thêm trụ {pole.PoleCode} vào trạm.");
            }

            await _context.SaveChangesAsync();

            TempData["StationRequestSuccess"] = $"Đã thêm trực tiếp {validPoles.Count} trụ vào hệ thống.";
            return RedirectToAction(nameof(MyStations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUpdatePoleRequest(UpdatePoleRequestViewModel model, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var redirectAction = RedirectToAction(nameof(Details), new { id = model.StationId, historyPage, polePage });

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == model.StationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Bạn không có quyền cập nhật trụ của trạm này.";
                return redirectAction;
            }

            if (!CanOwnerManageStation(station))
            {
                TempData["StationRequestError"] = "Trạm chưa được duyệt/kích hoạt hoặc đang bị admin khóa nên không thể sửa trụ.";
                return redirectAction;
            }

            var pole = await _context.ChargingPoles
                .FirstOrDefaultAsync(x => x.Id == model.PoleId && x.StationId == model.StationId);

            if (pole == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trụ cần cập nhật.";
                return redirectAction;
            }

            if (!ModelState.IsValid)
            {
                TempData["StationRequestError"] = "Dữ liệu cập nhật trụ chưa hợp lệ.";
                return redirectAction;
            }

            var allowedStatuses = new[]
            {
                ChargingStatus.Active,
                ChargingStatus.Inactive,
                ChargingStatus.Maintenance,
                ChargingStatus.Error,
                ChargingStatus.Error
            };

            if (!allowedStatuses.Contains(model.RequestedStatus))
            {
                TempData["StationRequestError"] = "Trạng thái trụ không hợp lệ.";
                return redirectAction;
            }

            var normalizedCode = (model.PoleCode ?? string.Empty).Trim();
            var duplicatedPole = await _context.ChargingPoles
                .AnyAsync(x => x.StationId == station.Id && x.Id != pole.Id && x.PoleCode == normalizedCode);

            if (duplicatedPole)
            {
                TempData["StationRequestError"] = $"Mã trụ {normalizedCode} đã tồn tại ở trạm này.";
                return redirectAction;
            }

            var oldPoleValue = $"{pole.PoleCode} | {pole.MaxPower} | {pole.Status} | {pole.Note}";
            pole.PoleCode = normalizedCode;
            pole.MaxPower = ChargingStatus.NormalizeKw(model.MaxPower);
            pole.Status = ChargingStatus.NormalizeNodeStatus(model.RequestedStatus);
            pole.Note = model.Note?.Trim();
            var newPoleValue = $"{pole.PoleCode} | {pole.MaxPower} | {pole.Status} | {pole.Note}";

            _context.StationOperationRequests.Add(new StationOperationRequest
            {
                StationId = station.Id,
                UserId = currentUserId.Value,
                RequestType = StationOperationRequestType.UpdatePole,
                PoleId = pole.Id,
                PoleCode = pole.PoleCode,
                PoleMaxPower = pole.MaxPower,
                RequestedPoleStatus = pole.Status,
                UserNote = model.Note?.Trim(),
                Status = StationWorkflowStatus.Completed,
                AdminNote = "Chủ trạm tự cập nhật trụ trực tiếp, không cần admin duyệt.",
                CreatedAt = DateTime.Now,
                ReviewedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            });

            LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.UpdateChargingPole, oldPoleValue, newPoleValue, model.Note?.Trim());

            await _context.SaveChangesAsync();
            TempData["StationRequestSuccess"] = "Đã cập nhật trụ trực tiếp.";
            return redirectAction;
        }

        [HttpGet]
        public async Task<IActionResult> RequestAddPole(int stationId, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var station = await _context.ChargingStations
                .Include(x => x.ChargingPoles)
                .FirstOrDefaultAsync(x => x.Id == stationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm hoặc bạn không có quyền thêm trụ cho trạm này.";
                return RedirectToAction(nameof(MyStations));
            }

            var model = new RequestAddPolePageViewModel
            {
                StationId = station.Id,
                StationName = station.Name ?? string.Empty,
                StationAddress = station.Address ?? string.Empty,
                StationStatus = StationOperationStatus.ToDisplay(station.OperationStatus),
                ExistingPoleCount = station.ChargingPoles.Count,
                HistoryPage = Math.Max(1, historyPage),
                PolePage = Math.Max(1, polePage)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RequestUpdatePole(int stationId, int poleId, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == stationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm hoặc bạn không có quyền cập nhật trụ của trạm này.";
                return RedirectToAction(nameof(MyStations));
            }

            var pole = await _context.ChargingPoles
                .FirstOrDefaultAsync(x => x.Id == poleId && x.StationId == stationId);

            if (pole == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trụ cần cập nhật.";
                return RedirectToAction(nameof(Details), new { id = stationId, historyPage, polePage });
            }

            var model = new RequestUpdatePolePageViewModel
            {
                StationId = station.Id,
                PoleId = pole.Id,
                StationName = station.Name ?? string.Empty,
                StationAddress = station.Address ?? string.Empty,
                StationStatus = StationOperationStatus.ToDisplay(station.OperationStatus),
                PoleCode = pole.PoleCode ?? string.Empty,
                MaxPower = pole.MaxPower,
                RequestedStatus = pole.Status ?? string.Empty,
                CurrentPoleStatus = pole.Status ?? string.Empty,
                CurrentPolePower = pole.MaxPower,
                CurrentPoleNote = pole.Note,
                Note = pole.Note,
                HistoryPage = Math.Max(1, historyPage),
                PolePage = Math.Max(1, polePage)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RequestDeletePole(int stationId, int poleId, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == stationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm hoặc bạn không có quyền xóa trụ của trạm này.";
                return RedirectToAction(nameof(MyStations));
            }

            var pole = await _context.ChargingPoles
                .FirstOrDefaultAsync(x => x.Id == poleId && x.StationId == stationId);

            if (pole == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trụ cần xóa.";
                return RedirectToAction(nameof(Details), new { id = stationId, historyPage, polePage });
            }

            var model = new RequestDeletePolePageViewModel
            {
                StationId = station.Id,
                PoleId = pole.Id,
                StationName = station.Name ?? string.Empty,
                PoleCode = pole.PoleCode ?? string.Empty,
                PoleStatus = pole.Status ?? string.Empty,
                PoleMaxPower = pole.MaxPower,
                PoleNote = pole.Note,
                HistoryPage = Math.Max(1, historyPage),
                PolePage = Math.Max(1, polePage)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDeletePoleRequest(DeletePoleRequestViewModel model, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var redirectAction = RedirectToAction(nameof(Details), new { id = model.StationId, historyPage, polePage });

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == model.StationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Bạn không có quyền xóa trụ của trạm này.";
                return redirectAction;
            }

            if (!CanOwnerManageStation(station))
            {
                TempData["StationRequestError"] = "Trạm chưa được duyệt/kích hoạt hoặc đang bị admin khóa nên không thể xóa trụ.";
                return redirectAction;
            }

            var pole = await _context.ChargingPoles
                .FirstOrDefaultAsync(x => x.Id == model.PoleId && x.StationId == model.StationId);

            if (pole == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trụ cần xóa.";
                return redirectAction;
            }

            _context.StationOperationRequests.Add(new StationOperationRequest
            {
                StationId = station.Id,
                UserId = currentUserId.Value,
                RequestType = StationOperationRequestType.DeletePole,
                PoleId = pole.Id,
                PoleCode = pole.PoleCode,
                PoleMaxPower = pole.MaxPower,
                RequestedPoleStatus = pole.Status,
                UserNote = model.Note?.Trim(),
                Status = StationWorkflowStatus.Completed,
                AdminNote = "Chủ trạm tự xóa trụ trực tiếp, không cần admin duyệt.",
                CreatedAt = DateTime.Now,
                ReviewedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            });

            var deletedPoleValue = $"{pole.PoleCode} | {pole.MaxPower} | {pole.Status}";
            _context.ChargingPoles.Remove(pole);

            var remainingPoleCount = await _context.ChargingPoles.CountAsync(x => x.StationId == station.Id && x.Id != pole.Id);
            if (remainingPoleCount <= 0)
            {
                station.OperationStatus = StationOperationStatus.Inactive;
                station.Status = ChargingStatus.Inactive;
            }

            LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.DeleteChargingPole, deletedPoleValue, null, model.Note?.Trim());

            await _context.SaveChangesAsync();
            TempData["StationRequestSuccess"] = "Đã xóa trụ trực tiếp khỏi hệ thống.";
            return redirectAction;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartMaintenancePayment(int id, bool returnToDetails = false, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm cần thanh toán phí duy trì.";
                return RedirectToAction(nameof(MyStations));
            }

            RefreshMaintenancePaymentStatus(station);

            if (StationSystemStatus.Normalize(station.SystemStatus) == StationSystemStatus.Locked)
            {
                TempData["StationRequestError"] = "Trạm đang bị admin khóa nên chưa thể thanh toán phí duy trì.";
                return ResolveStationRedirect(returnToDetails, station.Id, historyPage, polePage);
            }

            var transaction = new PaymentTransaction
            {
                UserId = currentUserId.Value,
                StationId = station.Id,
                PaymentType = PaymentTransactionType.Maintenance,
                Status = PaymentTransactionStatus.Pending,
                Amount = StationMaintenanceService.MonthlyMaintenanceFeeAmount,
                PayOsOrderCode = await GenerateUniquePayOsOrderCodeAsync(),
                Description = $"Thanh toán phí duy trì trạm {station.Name}",
                CreatedAt = DateTime.Now
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            var returnUrl = Url.Action(nameof(MaintenancePaymentReturn), "StationRegistration", new
            {
                area = "User",
                transactionId = transaction.Id,
                stationId = station.Id,
                returnToDetails,
                historyPage,
                polePage
            }, Request.Scheme) ?? string.Empty;

            var cancelUrl = returnUrl;

            var fallbackUrl = Url.Action(nameof(DemoCompleteMaintenance), "StationRegistration", new
            {
                area = "User",
                transactionId = transaction.Id,
                returnToDetails,
                historyPage,
                polePage
            }, Request.Scheme) ?? string.Empty;

            var payment = await _payOsCheckoutService.CreateMaintenancePaymentAsync(transaction, returnUrl, cancelUrl, fallbackUrl);

            transaction.PayOsOrderCode = payment.orderCode;
            transaction.PayOsCheckoutUrl = payment.checkoutUrl;
            transaction.Status = payment.isFallback ? "Chờ thanh toán demo" : PaymentTransactionStatus.Pending;

            await _context.SaveChangesAsync();

            return Redirect(payment.checkoutUrl);
        }

        [HttpGet]
        public async Task<IActionResult> MaintenancePaymentReturn(int? transactionId, int? stationId, long? orderCode, string? status, string? code, bool? cancel, bool returnToDetails = false, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var transaction = await FindMaintenanceTransactionAsync(currentUserId.Value, transactionId, orderCode);
            if (transaction == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy giao dịch phí duy trì.";
                return RedirectToAction(nameof(MyStations));
            }

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == transaction.StationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm của giao dịch phí duy trì.";
                return RedirectToAction(nameof(MyStations));
            }

            var normalizedStatus = (status ?? string.Empty).Trim().ToUpperInvariant();
            var isPaid = normalizedStatus == "PAID" && cancel != true && string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);
            var isCancelled = cancel == true || normalizedStatus == "CANCELLED";

            if (isPaid)
            {
                CompleteMaintenancePayment(station, transaction);
                LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.MaintenancePaid, null, station.MaintenanceFeeDueDate?.ToString("dd/MM/yyyy HH:mm"), "Thanh toán phí duy trì thành công.");
                TempData["StationRequestSuccess"] = $"Thanh toán phí duy trì thành công {MonthlyMaintenanceFeeAmount:N0}đ. Hạn duy trì mới: {station.MaintenanceFeeDueDate:dd/MM/yyyy HH:mm}.";
            }
            else if (isCancelled)
            {
                transaction.Status = PaymentTransactionStatus.Cancelled;
                transaction.CancelledAt = DateTime.Now;
                transaction.Note = "Người dùng hủy giao dịch phí duy trì trên PayOS.";
                RefreshMaintenancePaymentStatus(station);
                TempData["StationRequestError"] = "Bạn đã hủy thanh toán phí duy trì. Có thể thanh toán lại bất kỳ lúc nào.";
            }
            else
            {
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.Note = "PayOS chưa trả về trạng thái thanh toán thành công.";
                RefreshMaintenancePaymentStatus(station);
                TempData["StationRequestError"] = "Chưa ghi nhận thanh toán phí duy trì thành công.";
            }

            await _context.SaveChangesAsync();
            return ResolveStationRedirect(returnToDetails, station.Id, historyPage, polePage);
        }

        [HttpGet]
        public async Task<IActionResult> DemoCompleteMaintenance(int transactionId, bool returnToDetails = false, int historyPage = 1, int polePage = 1)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "User" });
            }

            var transaction = await _context.PaymentTransactions
                .FirstOrDefaultAsync(x =>
                    x.Id == transactionId &&
                    x.UserId == currentUserId.Value &&
                    (x.PaymentType == PaymentTransactionType.Maintenance || x.PaymentType == PaymentTransactionType.LegacyMaintenance));

            if (transaction == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy giao dịch demo phí duy trì.";
                return RedirectToAction(nameof(MyStations));
            }

            var station = await _context.ChargingStations
                .FirstOrDefaultAsync(x => x.Id == transaction.StationId && x.OwnerUserId == currentUserId.Value);

            if (station == null)
            {
                TempData["StationRequestError"] = "Không tìm thấy trạm của giao dịch phí duy trì.";
                return RedirectToAction(nameof(MyStations));
            }

            CompleteMaintenancePayment(station, transaction);
            LogStationActivity(station.Id, currentUserId.Value, StationActivityActionType.MaintenancePaid, null, station.MaintenanceFeeDueDate?.ToString("dd/MM/yyyy HH:mm"), "Thanh toán demo phí duy trì thành công.");
            await _context.SaveChangesAsync();

            TempData["StationRequestSuccess"] = $"Đã thanh toán demo phí duy trì {MonthlyMaintenanceFeeAmount:N0}đ. Hạn duy trì mới: {station.MaintenanceFeeDueDate:dd/MM/yyyy HH:mm}.";
            return ResolveStationRedirect(returnToDetails, station.Id, historyPage, polePage);
        }

        private async Task CompletePaidStationRequestAsync(StationRegistrationRequest request, PaymentTransaction? transaction = null)
        {
            if (request.CreatedStationId.HasValue)
            {
                return;
            }

            var maintenancePaidAt = DateTime.Now;
            var maintenanceDueDate = StationMaintenanceService.GetNextDueDate(maintenancePaidAt);

            var station = new ChargingStation
            {
                Name = request.StationName,
                Address = request.Address,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Status = ChargingStatus.Active,
                SystemStatus = StationSystemStatus.Approved,
                OperationStatus = StationOperationStatus.Active,
                OwnerUserId = request.UserId,
                ChargerType = request.InitialPoleChargerType,
                Power = request.InitialPoleMaxPower,
                PricePerKwh = 0,
                MonthlyMaintenanceFee = StationMaintenanceService.MonthlyMaintenanceFeeAmount,
                LastMaintenancePaidAt = maintenancePaidAt,
                MaintenancePaidUntil = maintenanceDueDate,
                MaintenancePaymentStatus = StationMaintenanceStatus.DisplayPaid,
                MaintenanceFeeStatus = StationMaintenanceStatus.Paid,
                MaintenanceFeePaidAt = maintenancePaidAt,
                MaintenanceFeeDueDate = maintenanceDueDate,
                MaintenanceFeeGraceUntil = null,
                IsVisible = true,
                HiddenReason = null
            };

            _context.ChargingStations.Add(station);
            await _context.SaveChangesAsync();

            if (request.InitialPoleCount > 0)
            {
                for (var index = 1; index <= request.InitialPoleCount; index++)
                {
                    _context.ChargingPoles.Add(new ChargingPole
                    {
                        StationId = station.Id,
                        PoleCode = $"TRU-{index:00}",
                        ChargerType = request.InitialPoleChargerType,
                        MaxPower = request.InitialPoleMaxPower,
                        Status = ChargingStatus.Active,
                        Note = request.InitialPoleNote,
                        SortOrder = index
                    });
                }

                await _context.SaveChangesAsync();
            }

            request.PaymentStatus = "Đã thanh toán";
            request.ApprovalStatus = StationWorkflowStatus.Completed;
            request.PaidAt = DateTime.Now;
            request.CompletedAt = DateTime.Now;
            request.CreatedStationId = station.Id;

            LogStationActivity(station.Id, request.UserId, StationActivityActionType.RegistrationPaid, null, station.Name, "Thanh toán phí đăng ký thành công, hệ thống tạo/kích hoạt trạm chính thức.");

            if (transaction != null)
            {
                transaction.StationId = station.Id;
                transaction.Amount = request.FeeAmount <= 0 ? InitialRegistrationFee : request.FeeAmount;
                transaction.Status = PaymentTransactionStatus.Paid;
                transaction.PaidAt = DateTime.Now;
                transaction.Note = "Thanh toán lần đầu thành công, hệ thống đã tạo trạm.";
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string?> SaveRequestImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowedExtensions.Contains(extension))
            {
                return null;
            }

            var folder = Path.Combine(_environment.WebRootPath, "uploads", "station-requests");
            Directory.CreateDirectory(folder);

            // Why changed: store request evidence images in a dedicated folder.
            var fileName = $"station-request-{Guid.NewGuid():N}{extension}";
            var savePath = Path.Combine(folder, fileName);

            await using var stream = new FileStream(savePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return $"/uploads/station-requests/{fileName}";
        }

        private async Task<MyStationDashboardViewModel> BuildMyStationDashboardAsync(int currentUserId)
        {
            var stations = await _context.ChargingStations
                .Where(x => x.OwnerUserId == currentUserId)
                .Include(x => x.ChargingPoles)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            foreach (var station in stations)
            {
                RefreshMaintenancePaymentStatus(station);
            }

            await _context.SaveChangesAsync();

            return new MyStationDashboardViewModel
            {
                Stations = stations,

                RegistrationRequests = await _context.StationRegistrationRequests
                    .Where(x => x.UserId == currentUserId)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync(),

                OperationRequests = await _context.StationOperationRequests
                    .Where(x => x.UserId == currentUserId)
                    .Include(x => x.Station)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync()
            };
        }


        private async Task<ManageStationDetailsViewModel?> BuildManageStationDetailsAsync(int stationId, int currentUserId, int historyPage = 1, int polePage = 1)
        {
            const int historyPageSize = 4;
            const int polePageSize = 4;

            var station = await _context.ChargingStations
                .Where(x => x.Id == stationId && x.OwnerUserId == currentUserId)
                .Include(x => x.ChargingPoles)
                .FirstOrDefaultAsync();

            if (station == null)
            {
                return null;
            }

            RefreshMaintenancePaymentStatus(station);
            await _context.SaveChangesAsync();

            var latestPhone = await _context.StationRegistrationRequests
                .Where(x => x.CreatedStationId == station.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.ContactPhone)
                .FirstOrDefaultAsync();

            var operationRequests = await _context.StationOperationRequests
                .Where(x => x.StationId == station.Id && x.UserId == currentUserId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var allPoles = station.ChargingPoles
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new PoleItemViewModel
                {
                    Id = x.Id,
                    PoleCode = x.PoleCode,
                    MaxPower = x.MaxPower,
                    Status = StationOperationStatus.ToDisplay(x.Status),
                    Note = x.Note
                })
                .ToList();

            var safeHistoryPage = Math.Max(1, historyPage);
            var safePolePage = Math.Max(1, polePage);
            var historyTotalPages = Math.Max(1, (int)Math.Ceiling(operationRequests.Count / (double)historyPageSize));
            var poleTotalPages = Math.Max(1, (int)Math.Ceiling(allPoles.Count / (double)polePageSize));

            safeHistoryPage = Math.Min(safeHistoryPage, historyTotalPages);
            safePolePage = Math.Min(safePolePage, poleTotalPages);

            return new ManageStationDetailsViewModel
            {
                StationId = station.Id,
                Name = station.Name ?? string.Empty,
                Address = station.Address ?? string.Empty,
                Status = StationOperationStatus.ToDisplay(station.OperationStatus),
                SystemStatus = station.SystemStatus,
                OperationStatus = station.OperationStatus,
                Latitude = station.Latitude,
                Longitude = station.Longitude,
                PhoneNumber = string.IsNullOrWhiteSpace(latestPhone) ? "-" : latestPhone,
                TotalPoleCount = station.ChargingPoles.Count,
                MonthlyMaintenanceFee = station.MonthlyMaintenanceFee <= 0 ? MonthlyMaintenanceFeeAmount : station.MonthlyMaintenanceFee,
                LastMaintenancePaidAt = station.LastMaintenancePaidAt,
                MaintenancePaidUntil = station.MaintenancePaidUntil,
                MaintenancePaymentStatus = string.IsNullOrWhiteSpace(station.MaintenancePaymentStatus) ? "Chưa đến hạn" : station.MaintenancePaymentStatus,
                MaintenanceFeeStatus = station.MaintenanceFeeStatus,
                MaintenanceFeeDueDate = station.MaintenanceFeeDueDate,
                MaintenanceFeePaidAt = station.MaintenanceFeePaidAt,
                MaintenanceFeeGraceUntil = station.MaintenanceFeeGraceUntil,
                IsVisible = station.IsVisible,
                HiddenReason = station.HiddenReason,
                LatestOperationAt = operationRequests.FirstOrDefault()?.CreatedAt,
                Poles = allPoles,
                OperationRequests = operationRequests,

                HistoryItems = operationRequests
                    .Skip((safeHistoryPage - 1) * historyPageSize)
                    .Take(historyPageSize)
                    .ToList(),
                HistoryPage = safeHistoryPage,
                HistoryPageSize = historyPageSize,
                HistoryTotalCount = operationRequests.Count,

                PoleItems = allPoles
                    .Skip((safePolePage - 1) * polePageSize)
                    .Take(polePageSize)
                    .ToList(),
                PolePage = safePolePage,
                PolePageSize = polePageSize,
                PoleTotalCount = allPoles.Count
            };
        }

        private static void RefreshMaintenancePaymentStatus(ChargingStation station)
        {
            StationMaintenanceService.Refresh(station);
        }

        private static void CompleteMaintenancePayment(ChargingStation station, PaymentTransaction transaction)
        {
            var now = DateTime.Now;

            transaction.Amount = StationMaintenanceService.MonthlyMaintenanceFeeAmount;
            transaction.Status = PaymentTransactionStatus.Paid;
            transaction.PaidAt = now;
            transaction.Note = "Thanh toán phí duy trì thành công.";

            StationMaintenanceService.MarkPaid(station, transaction.Id, now);
            // Log is written by caller through DbContext when the transaction is saved.
        }

        private async Task<PaymentTransaction?> FindInitialRegistrationTransactionAsync(StationRegistrationRequest request, long? orderCode)
        {
            var initialPaymentTypes = new[] { PaymentTransactionType.InitialRegistration, PaymentTransactionType.LegacyInitialRegistration };

            var query = _context.PaymentTransactions
                .Where(x =>
                    x.RegistrationRequestId == request.Id &&
                    initialPaymentTypes.Contains(x.PaymentType));

            if (orderCode.HasValue)
            {
                query = query.Where(x => x.PayOsOrderCode == orderCode.Value || x.PayOsOrderCode == request.PayOsOrderCode);
            }

            return await query
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<PaymentTransaction?> FindMaintenanceTransactionAsync(int userId, int? transactionId, long? orderCode)
        {
            var maintenancePaymentTypes = new[] { PaymentTransactionType.Maintenance, PaymentTransactionType.LegacyMaintenance };

            var query = _context.PaymentTransactions
                .Where(x => x.UserId == userId && maintenancePaymentTypes.Contains(x.PaymentType));

            if (transactionId.HasValue)
            {
                query = query.Where(x => x.Id == transactionId.Value);
            }
            else if (orderCode.HasValue)
            {
                query = query.Where(x => x.PayOsOrderCode == orderCode.Value);
            }
            else
            {
                return null;
            }

            return await query.FirstOrDefaultAsync();
        }

        private async Task<long> GenerateUniquePayOsOrderCodeAsync()
        {
            var baseCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (var i = 0; i < 100; i++)
            {
                var candidate = baseCode + i;
                var existsInTransactions = await _context.PaymentTransactions.AnyAsync(x => x.PayOsOrderCode == candidate);
                var existsInRequests = await _context.StationRegistrationRequests.AnyAsync(x => x.PayOsOrderCode == candidate);

                if (!existsInTransactions && !existsInRequests)
                {
                    return candidate;
                }
            }

            return long.Parse(DateTime.Now.ToString("yyMMddHHmmssfff"));
        }

        private static bool CanOwnerManageStation(ChargingStation station)
        {
            return StationSystemStatus.Normalize(station.SystemStatus) == StationSystemStatus.Approved
                && StationMaintenanceStatus.Normalize(station.MaintenanceFeeStatus) != StationMaintenanceStatus.Locked;
        }

        private static bool IsAllowedOperationStatusInput(string? status)
        {
            var normalized = StationOperationStatus.Normalize(status);
            return normalized == StationOperationStatus.Active
                || normalized == StationOperationStatus.Error
                || normalized == StationOperationStatus.Maintenance
                || normalized == StationOperationStatus.Inactive;
        }

        private void LogStationActivity(int stationId, int? userId, string actionType, string? oldValue, string? newValue, string? description)
        {
            _context.StationActivityLogs.Add(new StationActivityLog
            {
                StationId = stationId,
                UserId = userId,
                ActionType = actionType,
                OldValue = oldValue,
                NewValue = newValue,
                Description = description,
                CreatedAt = DateTime.Now
            });
        }

        private IActionResult ResolveStationRedirect(bool returnToDetails, int stationId, int historyPage = 1, int polePage = 1)
        {
            // Why changed: keep the user on the same detail page and preserve pagination after submitting actions.
            return returnToDetails
                ? RedirectToAction(nameof(Details), new { id = stationId, historyPage, polePage })
                : RedirectToAction(nameof(MyStations));
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return null;
            }

            return await _context.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUserId.Value);
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
