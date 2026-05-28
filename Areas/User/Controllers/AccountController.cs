using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.User.ViewModels;
using tramsac99.Data;
using tramsac99.Services;

namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IEmailSender _emailSender;

        public AccountController(
            AppDbContext context,
            IWebHostEnvironment environment,
            IEmailSender emailSender)
        {
            _context = context;
            _environment = environment;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var model = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var input = model.UserNameOrEmail?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                ModelState.AddModelError("", "Vui lòng nhập tên đăng nhập hoặc email.");
                return View(model);
            }

            var user = await _context.AppUsers.FirstOrDefaultAsync(x =>
                x.Username == input || x.Email == input);

            if (user == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View(model);
            }

            if (user.IsBlocked)
            {
                ModelState.AddModelError("", "Tài khoản đã bị khóa.");
                return View(model);
            }

            if (!await VerifyPasswordAndUpgradeIfNeededAsync(user, model.Password))
            {
                ModelState.AddModelError("", "Sai mật khẩu.");
                return View(model);
            }

            await SignInUserAsync(user, model.RememberMe);

            if (user.Role == "Admin")
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home", new { area = "User" });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Vui lòng nhập email.");
                return View(model);
            }

            var normalizedEmail = email.ToUpperInvariant();
            var user = await _context.AppUsers.FirstOrDefaultAsync(x =>
                x.Email != null && x.Email.ToUpper() == normalizedEmail);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Email không tồn tại trong hệ thống.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (user.IsBlocked)
            {
                TempData["ErrorMessage"] = "Tài khoản gắn với email này đang bị khóa.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var now = DateTime.Now;
            var token = GenerateSecureToken();

            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(60),
                RequestedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            var resetUrl = BuildResetPasswordUrl(token);
            var safeResetUrl = System.Net.WebUtility.HtmlEncode(resetUrl);

            var displayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
            var safeDisplayName = System.Net.WebUtility.HtmlEncode(displayName ?? user.Email);

            var htmlBody = $@"
<div style='font-family:Arial,sans-serif;line-height:1.6;color:#1f2a26'>
    <h2 style='margin:0 0 12px;color:#159b63'>Khôi phục mật khẩu TramSac99</h2>
    <p>Xin chào <strong>{safeDisplayName}</strong>,</p>
    <p>Bạn vừa gửi yêu cầu quên mật khẩu cho tài khoản TramSac99.</p>
    <p>Nhấn vào nút bên dưới để đặt lại mật khẩu. Liên kết này có hiệu lực trong <strong>60 phút</strong>.</p>
    <p style='margin:24px 0'>
        <a href='{safeResetUrl}' style='display:inline-block;padding:12px 20px;background:#16a05f;color:#ffffff;text-decoration:none;border-radius:999px;font-weight:700'>Đặt lại mật khẩu</a>
    </p>
    <p>Nếu nút không bấm được, hãy copy toàn bộ link này vào trình duyệt:</p>
    <p style='word-break:break-all'><a href='{safeResetUrl}'>{safeResetUrl}</a></p>
    <p>Nếu bạn không thực hiện yêu cầu này, bạn có thể bỏ qua email.</p>
    <hr style='border:none;border-top:1px solid #e5ece8;margin:20px 0' />
    <p style='font-size:13px;color:#6b7d75'>Email này được gửi tự động từ hệ thống TramSac99.</p>
</div>";

            try
            {
                await _emailSender.SendAsync(user.Email, "[TramSac99] Đặt lại mật khẩu", htmlBody);
            }
            catch (Exception ex)
            {
                _context.PasswordResetTokens.Remove(resetToken);
                await _context.SaveChangesAsync();

                ModelState.AddModelError("", $"Không gửi được email đặt lại mật khẩu. {ex.Message}");
                return View(model);
            }

            TempData["SuccessMessage"] = "Liên kết đặt lại mật khẩu đã được gửi về Gmail của bạn. Link có hiệu lực trong 60 phút.";

            // Chỉ hiển thị link trực tiếp khi chạy Development để dễ test trên máy cá nhân.
            // Khi đưa lên production, phần này tự ẩn.
            if (_environment.IsDevelopment())
            {
                TempData["DevResetUrl"] = resetUrl;
            }

            return RedirectToAction(nameof(ForgotPassword));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token)
        {
            var normalizedToken = NormalizeResetToken(token);
            if (normalizedToken == null)
            {
                TempData["ErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var resetToken = await _context.PasswordResetTokens
                .Include(x => x.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Token == normalizedToken);

            var errorMessage = GetResetTokenErrorMessage(resetToken, DateTime.Now);
            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel { Token = normalizedToken });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var normalizedToken = NormalizeResetToken(model.Token);
            if (normalizedToken == null)
            {
                ModelState.AddModelError("", "Liên kết đặt lại mật khẩu không hợp lệ.");
                return View(model);
            }

            model.Token = normalizedToken;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var resetToken = await _context.PasswordResetTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == normalizedToken);

            var now = DateTime.Now;
            var errorMessage = GetResetTokenErrorMessage(resetToken, now);
            if (errorMessage != null)
            {
                ModelState.AddModelError("", errorMessage);
                return View(model);
            }

            resetToken!.User!.PasswordHash = HashPassword(resetToken.User, model.NewPassword);
            resetToken.UsedAt = now;

            // Sau khi đổi mật khẩu thành công mới vô hiệu hóa các link khác của cùng tài khoản.
            var otherTokens = await _context.PasswordResetTokens
                .Where(x => x.UserId == resetToken.UserId && x.Id != resetToken.Id && x.UsedAt == null)
                .ToListAsync();

            foreach (var item in otherTokens)
            {
                item.UsedAt = now;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = model.Username?.Trim();
            var email = model.Email?.Trim();
            var fullName = model.FullName?.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập không được để trống.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email không được để trống.");
                return View(model);
            }

            var existedUserName = await _context.AppUsers.AnyAsync(x => x.Username == username);
            if (existedUserName)
            {
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại.");
                return View(model);
            }

            var existedEmail = await _context.AppUsers.AnyAsync(x => x.Email == email);
            if (existedEmail)
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
                return View(model);
            }

            var user = new AppUser
            {
                Username = username,
                Email = email,
                FullName = fullName,
                Role = "User",
                IsBlocked = false,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = HashPassword(user, model.Password);

            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đăng ký thành công. Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            return RedirectToAction(nameof(UserInfo));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> UserInfo()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login), new
                {
                    area = "User",
                    returnUrl = Url.Action(nameof(UserInfo), "Account", new { area = "User" })
                });
            }

            var model = await BuildProfileViewModelAsync(user);
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ReviewHistory()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login), new
                {
                    area = "User",
                    returnUrl = Url.Action(nameof(ReviewHistory), "Account", new { area = "User" })
                });
            }

            var model = await BuildProfileViewModelAsync(user);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var pageModel = await BuildProfileViewModelAsync(user);
            pageModel.Email = model.Email?.Trim() ?? string.Empty;
            pageModel.FullName = model.FullName?.Trim();

            if (string.IsNullOrWhiteSpace(pageModel.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email không được để trống.");
            }
            else
            {
                var duplicatedEmail = await _context.AppUsers.AnyAsync(x => x.Email == pageModel.Email && x.Id != user.Id);
                if (duplicatedEmail)
                {
                    ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("UserInfo", pageModel);
            }

            user.Email = pageModel.Email;
            user.FullName = pageModel.FullName;

            await _context.SaveChangesAsync();
            TempData["ProfileSuccessMessage"] = "Đã cập nhật thông tin người dùng.";

            return RedirectToAction(nameof(UserInfo));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(IFormFile? avatarFile)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var pageModel = await BuildProfileViewModelAsync(user);

            if (avatarFile == null || avatarFile.Length == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ảnh đại diện.");
                return View("UserInfo", pageModel);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("", "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp.");
                return View("UserInfo", pageModel);
            }

            if (avatarFile.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError("", "Ảnh đại diện không được vượt quá 2MB.");
                return View("UserInfo", pageModel);
            }

            var avatarFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(avatarFolder);

            if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
            {
                var oldRelativePath = user.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var oldFullPath = Path.Combine(_environment.WebRootPath, oldRelativePath);

                if (System.IO.File.Exists(oldFullPath))
                {
                    System.IO.File.Delete(oldFullPath);
                }
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var savePath = Path.Combine(avatarFolder, fileName);

            await using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }

            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _context.SaveChangesAsync();

            TempData["AvatarSuccessMessage"] = "Đã cập nhật ảnh đại diện.";
            return RedirectToAction(nameof(UserInfo));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(UserProfileViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var pageModel = await BuildProfileViewModelAsync(user);

            if (string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Vui lòng nhập mật khẩu hiện tại.");
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới.");
            }

            if (string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
            {
                ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Vui lòng xác nhận mật khẩu mới.");
            }

            if (!string.IsNullOrWhiteSpace(model.NewPassword) &&
                !string.IsNullOrWhiteSpace(model.ConfirmNewPassword) &&
                model.NewPassword != model.ConfirmNewPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Xác nhận mật khẩu không khớp.");
            }

            if (!string.IsNullOrWhiteSpace(model.CurrentPassword) && !VerifyPassword(user, model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            }

            if (!ModelState.IsValid)
            {
                return View("UserInfo", pageModel);
            }

            user.PasswordHash = HashPassword(user, model.NewPassword!);
            await _context.SaveChangesAsync();

            TempData["PasswordSuccessMessage"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(UserInfo));
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReviewHistoryAjax([FromBody] UpdateReviewHistoryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu cập nhật đánh giá không hợp lệ."
                });
            }

            var user = await GetCurrentUserAsync();
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập để chỉnh sửa đánh giá."
                });
            }

            var review = await _context.StationReviews
                .Include(x => x.ChargingStation)
                .FirstOrDefaultAsync(x => x.Id == model.ReviewId && x.UserName == user.Username);

            if (review == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy đánh giá của bạn để chỉnh sửa."
                });
            }

            review.Rating = model.Rating;
            review.Comment = model.Comment?.Trim();
            review.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã cập nhật đánh giá thành công.",
                reviewId = review.Id,
                stationId = review.StationId,
                stationName = review.ChargingStation?.Name ?? string.Empty,
                rating = review.Rating,
                comment = review.Comment ?? string.Empty,
                activityAt = (review.UpdatedAt ?? review.CreatedAt).ToString("dd/MM/yyyy HH:mm"),
                isEdited = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home", new { area = "User" });
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task<UserProfileViewModel> BuildProfileViewModelAsync(AppUser user)
        {
            var reviewHistory = await _context.StationReviews
                .Where(x => x.UserName == user.Username)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Select(x => new UserReviewHistoryItemViewModel
                {
                    Id = x.Id,
                    StationId = x.StationId,
                    StationName = x.ChargingStation != null ? (x.ChargingStation.Name ?? "") : "",
                    Rating = x.Rating,
                    Comment = x.Comment,
                    ActivityAt = x.UpdatedAt ?? x.CreatedAt,
                    IsEdited = x.UpdatedAt != null
                })
                .ToListAsync();

            return new UserProfileViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                AvatarUrl = user.AvatarUrl,
                ReviewHistory = reviewHistory
            };
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(rawUserId, out var userId))
            {
                return null;
            }

            return await _context.AppUsers.FirstOrDefaultAsync(x => x.Id == userId);
        }

        private async Task SignInUserAsync(AppUser user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe
                        ? DateTimeOffset.UtcNow.AddDays(7)
                        : DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        private static string HashPassword(AppUser user, string password)
        {
            var hasher = new PasswordHasher<AppUser>();
            return hasher.HashPassword(user, password);
        }

        private static bool VerifyPassword(AppUser user, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
            {
                return true;
            }

            var hasher = new PasswordHasher<AppUser>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, password);
            return result == PasswordVerificationResult.Success
                || result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        private async Task<bool> VerifyPasswordAndUpgradeIfNeededAsync(AppUser user, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
            {
                user.PasswordHash = HashPassword(user, password);
                await _context.SaveChangesAsync();
                return true;
            }

            var hasher = new PasswordHasher<AppUser>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, password);

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, password);
                await _context.SaveChangesAsync();
                return true;
            }

            return result == PasswordVerificationResult.Success;
        }

        private string BuildResetPasswordUrl(string token)
        {
            return Url.Action(
                nameof(ResetPassword),
                "Account",
                new { area = "User", token },
                Request.Scheme) ?? string.Empty;
        }

        private static string? NormalizeResetToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var normalizedToken = token.Trim();

            try
            {
                normalizedToken = Uri.UnescapeDataString(normalizedToken);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(normalizedToken) || normalizedToken.Length > 200)
            {
                return null;
            }

            return normalizedToken;
        }

        private static string? GetResetTokenErrorMessage(PasswordResetToken? resetToken, DateTime now)
        {
            if (resetToken == null)
            {
                return "Liên kết đặt lại mật khẩu không tồn tại hoặc không đúng. Vui lòng gửi lại yêu cầu quên mật khẩu.";
            }

            if (resetToken.User == null)
            {
                return "Không tìm thấy tài khoản cần đặt lại mật khẩu.";
            }

            if (resetToken.User.IsBlocked)
            {
                return "Tài khoản đã bị khóa. Không thể đặt lại mật khẩu.";
            }

            if (resetToken.UsedAt != null)
            {
                return "Liên kết đặt lại mật khẩu đã được sử dụng. Vui lòng gửi lại yêu cầu quên mật khẩu và bấm email mới nhất.";
            }

            if (resetToken.ExpiresAt <= now)
            {
                return "Liên kết đặt lại mật khẩu đã hết hạn. Vui lòng gửi lại yêu cầu quên mật khẩu.";
            }

            return null;
        }

        private static string GenerateSecureToken()
        {
            // Token dạng hex chỉ gồm 0-9 và a-f nên không bị lỗi khi đưa lên URL/email.
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }
    }
}
