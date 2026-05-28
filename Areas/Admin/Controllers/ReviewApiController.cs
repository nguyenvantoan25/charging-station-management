using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.Admin.Models.Dto;
using tramsac99.Data;

namespace mvc.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/api/reviews")]
    [ApiController]
    public class ReviewApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReviewApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("station/{stationId}")]
        public async Task<IActionResult> GetReviewsByStation(int stationId)
        {
            var stationExists = await _context.ChargingStations.AnyAsync(x => x.Id == stationId);
            if (!stationExists)
                return NotFound(new { message = "Trạm không tồn tại." });

            var reviews = await _context.StationReviews
                .Where(r => r.StationId == stationId)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt) // Why changed: show edited reviews in newest order.
                .Select(r => new
                {
                    r.Id,
                    r.StationId,
                    r.Rating,
                    r.Comment,
                    r.UserName,
                    r.CreatedAt,
                    r.UpdatedAt,
                    isEdited = r.UpdatedAt != null
                })
                .ToListAsync();

            return Ok(reviews);
        }

        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var station = await _context.ChargingStations.FirstOrDefaultAsync(x => x.Id == request.StationId);
            if (station == null)
                return NotFound(new { message = "Trạm không tồn tại." });

            // Why changed: admin API also updates the existing review of the same user on the same station.
            var existedReview = await _context.StationReviews
                .FirstOrDefaultAsync(x => x.StationId == request.StationId && x.UserName == request.UserName);

            string message;

            if (existedReview == null)
            {
                var review = new StationReview
                {
                    StationId = request.StationId,
                    Rating = request.Rating,
                    UserName = request.UserName,
                    Comment = request.Comment,
                    CreatedAt = DateTime.Now
                };

                _context.StationReviews.Add(review);
                message = "Đánh giá đã được gửi thành công.";
            }
            else
            {
                existedReview.Rating = request.Rating;
                existedReview.Comment = request.Comment;
                existedReview.UpdatedAt = DateTime.Now;
                message = "Đánh giá đã được cập nhật thành công.";
            }

            await _context.SaveChangesAsync();

            return Ok(new { message });
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentReviews()
        {
            var twoDaysAgo = DateTime.Now.AddDays(-2);

            // Why changed: use UpdatedAt when available so edited reviews also appear in recent activity.
            var reviews = await _context.StationReviews
                .Where(r => (r.UpdatedAt ?? r.CreatedAt) >= twoDaysAgo)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.StationId,
                    stationName = r.ChargingStation != null ? r.ChargingStation.Name : "",
                    r.UserName,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    r.UpdatedAt,
                    activityAt = r.UpdatedAt ?? r.CreatedAt,
                    isEdited = r.UpdatedAt != null
                })
                .ToListAsync();

            return Ok(reviews);
        }
    }
}
