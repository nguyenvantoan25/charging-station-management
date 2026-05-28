using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Data;

namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    [Route("User/api/favorites")]
    [Authorize]
    public class FavoriteApiController : Controller
    {
        private readonly AppDbContext _context;

        public FavoriteApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("{stationId}/toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavorite(int stationId)
        {
            if (!await FavoriteTableExistsAsync())
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Bảng yêu thích chưa được tạo trong database. Hãy restart app để migration tự chạy."
                });
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập."
                });
            }

            var stationExists = await _context.ChargingStations.AnyAsync(x => x.Id == stationId);
            if (!stationExists)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy trạm."
                });
            }

            var favorite = await _context.FavoriteStations
                .FirstOrDefaultAsync(x => x.UserId == currentUserId.Value && x.StationId == stationId);

            bool isFavorite;
            string message;

            if (favorite == null)
            {
                _context.FavoriteStations.Add(new FavoriteStation
                {
                    UserId = currentUserId.Value,
                    StationId = stationId,
                    CreatedAt = DateTime.Now
                });

                isFavorite = true;
                message = "Đã thêm vào trạm yêu thích.";
            }
            else
            {
                _context.FavoriteStations.Remove(favorite);
                isFavorite = false;
                message = "Đã bỏ khỏi trạm yêu thích.";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isFavorite,
                message
            });
        }

        private async Task<bool> FavoriteTableExistsAsync()
        {
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[FavoriteStations]', N'U') IS NOT NULL THEN 1 ELSE 0 END";

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result ?? 0) == 1;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
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
