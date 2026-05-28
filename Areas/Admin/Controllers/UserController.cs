using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
    public class UserController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public UserController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ListData()
        {
            // Why changed: use SQL-safe filter to hide admin accounts from this screen.
            // Do NOT use string.Equals(..., StringComparison.OrdinalIgnoreCase) inside EF query.
            var users = await _context.AppUsers
                .Where(x => x.Role == null || x.Role != "Admin")
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Username,
                    x.Email,
                    x.FullName,
                    x.Role,
                    x.IsBlocked,
                    x.CreatedAt,
                    x.AvatarUrl,
                    reviewCount = _context.StationReviews.Count(r => r.UserName == x.Username)
                })
                .ToListAsync();

            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();

            var model = MapToEditViewModel(user);
            model.IsSelf = GetCurrentUserId() == user.Id;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            var user = await _context.AppUsers.FindAsync(model.Id);
            if (user == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var isSelf = currentUserId == user.Id;

            model.IsSelf = isSelf;
            model.AvatarUrl = user.AvatarUrl;
            model.CreatedAt = user.CreatedAt;

            model.Username = model.Username?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.FullName = model.FullName?.Trim();

            // Why changed: role is read-only on this screen for all accounts.
            model.Role = user.Role;

            if (string.IsNullOrWhiteSpace(model.Username))
            {
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email không được để trống.");
            }

            var duplicatedUserName = await _context.AppUsers.AnyAsync(x => x.Username == model.Username && x.Id != model.Id);
            if (duplicatedUserName)
            {
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại.");
            }

            var duplicatedEmail = await _context.AppUsers.AnyAsync(x => x.Email == model.Email && x.Id != model.Id);
            if (duplicatedEmail)
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            }

            if (isSelf && model.IsBlocked)
            {
                ModelState.AddModelError(nameof(model.IsBlocked), "Bạn không thể tự khóa tài khoản của chính mình.");
            }

            if (!string.IsNullOrWhiteSpace(model.NewPassword) || !string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
            {
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới.");
                }

                if (string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                {
                    ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Vui lòng xác nhận mật khẩu mới.");
                }

                if (!string.IsNullOrWhiteSpace(model.NewPassword) && model.NewPassword.Length < 6)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải có ít nhất 6 ký tự.");
                }

                if (!string.Equals(model.NewPassword, model.ConfirmNewPassword, StringComparison.Ordinal))
                {
                    ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Xác nhận mật khẩu không khớp.");
                }
            }

            if (model.AvatarFile != null)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(model.AvatarFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.AvatarFile), "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp.");
                }

                if (model.AvatarFile.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.AvatarFile), "Ảnh đại diện không được vượt quá 2MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            user.Username = model.Username;
            user.Email = model.Email;
            user.FullName = model.FullName;
            user.IsBlocked = isSelf ? false : model.IsBlocked;

            // Why changed: keep original role unchanged because the system only maintains one admin role.
            user.Role = user.Role;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                user.PasswordHash = HashPassword(user, model.NewPassword);
            }

            if (model.RemoveAvatar)
            {
                DeleteLocalAvatar(user.AvatarUrl);
                user.AvatarUrl = null;
            }

            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                DeleteLocalAvatar(user.AvatarUrl);
                user.AvatarUrl = await SaveAvatarAsync(model.AvatarFile);
            }

            await _context.SaveChangesAsync();

            if (isSelf)
            {
                await RefreshCurrentAdminSignInAsync(user);
            }

            TempData["SuccessMessage"] = isSelf
                ? "Đã cập nhật thông tin admin thành công."
                : "Đã cập nhật người dùng thành công.";

            return RedirectToAction(nameof(Edit), new { id = user.Id });
        }

        private static string HashPassword(AppUser user, string password)
        {
            var hasher = new PasswordHasher<AppUser>();
            return hasher.HashPassword(user, password);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBlock(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == id)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Bạn không thể tự khóa tài khoản của chính mình."
                });
            }

            var user = await _context.AppUsers.FindAsync(id);
            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy người dùng."
                });
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Không thể khóa tài khoản admin tại màn hình này."
                });
            }

            user.IsBlocked = !user.IsBlocked;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isBlocked = user.IsBlocked,
                message = user.IsBlocked ? "Đã khóa người dùng." : "Đã mở khóa người dùng."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == id)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Bạn không thể tự xóa tài khoản của chính mình."
                });
            }

            var user = await _context.AppUsers.FindAsync(id);
            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy người dùng."
                });
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Không thể xóa tài khoản admin tại màn hình này."
                });
            }

            var favoriteRows = await _context.FavoriteStations
                .Where(x => x.UserId == user.Id)
                .ToListAsync();

            var reviews = await _context.StationReviews
                .Where(x => x.UserName == user.Username)
                .ToListAsync();

            if (favoriteRows.Any())
            {
                _context.FavoriteStations.RemoveRange(favoriteRows);
            }

            if (reviews.Any())
            {
                _context.StationReviews.RemoveRange(reviews);
            }

            DeleteLocalAvatar(user.AvatarUrl);

            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã xóa người dùng."
            });
        }

        [HttpGet]
        public async Task<IActionResult> ReviewHistory(int id)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy người dùng."
                });
            }

            var reviews = await _context.StationReviews
                .Where(x => x.UserName == user.Username)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.StationId,
                    stationName = x.ChargingStation != null ? x.ChargingStation.Name : "",
                    x.Rating,
                    x.Comment,
                    activityAt = x.UpdatedAt ?? x.CreatedAt,
                    isEdited = x.UpdatedAt != null
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                userName = user.Username,
                reviews
            });
        }

        private UserEditViewModel MapToEditViewModel(AppUser user)
        {
            return new UserEditViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                IsBlocked = user.IsBlocked,
                CreatedAt = user.CreatedAt,
                AvatarUrl = user.AvatarUrl
            };
        }

        private async Task<string> SaveAvatarAsync(IFormFile avatarFile)
        {
            var avatarFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(avatarFolder);

            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var savePath = Path.Combine(avatarFolder, fileName);

            await using var stream = new FileStream(savePath, FileMode.Create);
            await avatarFile.CopyToAsync(stream);

            return $"/uploads/avatars/{fileName}";
        }

        private void DeleteLocalAvatar(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return;
            }

            if (!avatarUrl.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativePath = avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private async Task RefreshCurrentAdminSignInAsync(AppUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? "Admin")
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
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });
        }

        private int GetCurrentUserId()
        {
            var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(rawUserId, out var userId) ? userId : 0;
        }
    }
}
