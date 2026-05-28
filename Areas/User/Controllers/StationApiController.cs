using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.User.ViewModels;
using tramsac99.Data;
using tramsac99.Services;

namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    [Route("User/api/stations")]
    [ApiController]
    public class StationApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StationApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStations([FromQuery] string? keyword, [FromQuery] string? status, [FromQuery] string? location)
        {
            await RefreshPublicStationMaintenanceStatusesAsync();
            var currentUserId = GetCurrentUserId();
            var favoriteStationIds = await GetFavoriteStationIdsSafeAsync(currentUserId);

            var rows = await BuildStationQuery(favoriteStationIds, status).ToListAsync();

            // Why changed: run accent-insensitive smart filtering in memory so the search feels genuinely useful.
            var result = rows
                .Select(row => new
                {
                    Row = row,
                    KeywordScore = CalculateKeywordScore(keyword, row),
                    LocationMatched = MatchesLocation(location, row)
                })
                .Where(x => MatchesKeyword(keyword, x.KeywordScore) && x.LocationMatched)
                .OrderByDescending(x => x.KeywordScore)
                .ThenByDescending(x => x.Row.IsFavorite)
                .ThenByDescending(x => x.Row.ReviewCount)
                .ThenBy(x => x.Row.Name)
                .Select(x => MapToListItem(x.Row, x.KeywordScore, null))
                .ToList();

            return Ok(result);
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyStations(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 5,
            [FromQuery] string? keyword = null,
            [FromQuery] string? status = null,
            [FromQuery] string? location = null)
        {
            await RefreshPublicStationMaintenanceStatusesAsync();
            var currentUserId = GetCurrentUserId();
            var favoriteStationIds = await GetFavoriteStationIdsSafeAsync(currentUserId);

            var rows = await BuildStationQuery(favoriteStationIds, status).ToListAsync();

            var result = rows
                .Select(row =>
                {
                    var distanceKm = CalculateDistanceKm(lat, lng, row.Latitude, row.Longitude);
                    var keywordScore = CalculateKeywordScore(keyword, row);

                    return new
                    {
                        Row = row,
                        DistanceKm = distanceKm,
                        KeywordScore = keywordScore,
                        LocationMatched = MatchesLocation(location, row)
                    };
                })
                .Where(x => x.DistanceKm <= radiusKm && MatchesKeyword(keyword, x.KeywordScore) && x.LocationMatched)
                .OrderByDescending(x => x.KeywordScore)
                .ThenBy(x => x.DistanceKm)
                .ThenByDescending(x => x.Row.IsFavorite)
                .ThenBy(x => x.Row.Name)
                .Select(x => MapToListItem(x.Row, x.KeywordScore, x.DistanceKm))
                .ToList();

            return Ok(result);
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions([FromQuery] string? keyword)
        {
            await RefreshPublicStationMaintenanceStatusesAsync();
            keyword = (keyword ?? string.Empty).Trim();
            if (keyword.Length < 2)
            {
                return Ok(Array.Empty<object>());
            }

            var currentUserId = GetCurrentUserId();
            var favoriteStationIds = await GetFavoriteStationIdsSafeAsync(currentUserId);
            var rows = await BuildStationQuery(favoriteStationIds, status: null).ToListAsync();

            // Why changed: return top station suggestions for the typeahead dropdown on the user page.
            var suggestions = rows
                .Select(row => new
                {
                    Row = row,
                    Score = CalculateKeywordScore(keyword, row)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Row.IsFavorite)
                .ThenByDescending(x => x.Row.ReviewCount)
                .ThenBy(x => x.Row.Name)
                .Take(8)
                .Select(x => new
                {
                    id = x.Row.Id,
                    name = x.Row.Name,
                    address = x.Row.Address,
                    status = x.Row.Status,
                    chargerType = x.Row.ChargerType,
                    power = x.Row.Power,
                    subtitle = BuildSuggestionSubtitle(x.Row),
                    badge = BuildSuggestionBadge(x.Row)
                })
                .ToList();

            return Ok(suggestions);
        }

        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetStationReviews(int id)
        {
            var station = await _context.ChargingStations
                .Include(x => x.Reviews)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (station == null)
                return NotFound();

            var reviews = station.Reviews
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    id = x.Id,
                    rating = x.Rating,
                    comment = x.Comment,
                    userName = x.UserName,
                    createdAt = x.CreatedAt
                })
                .ToList();

            var breakdown = new Dictionary<string, int>
            {
                ["5"] = station.Reviews.Count(x => x.Rating == 5),
                ["4"] = station.Reviews.Count(x => x.Rating == 4),
                ["3"] = station.Reviews.Count(x => x.Rating == 3),
                ["2"] = station.Reviews.Count(x => x.Rating == 2),
                ["1"] = station.Reviews.Count(x => x.Rating == 1)
            };

            return Ok(new
            {
                averageRating = station.Reviews.Any() ? Math.Round(station.Reviews.Average(x => x.Rating), 1) : 0,
                reviewCount = station.Reviews.Count,
                breakdown,
                reviews
            });
        }
        private async Task RefreshPublicStationMaintenanceStatusesAsync()
        {
            var stations = await _context.ChargingStations
                .Where(x => x.OwnerUserId.HasValue)
                .ToListAsync();

            var hasChanged = false;

            foreach (var station in stations)
            {
                if (StationMaintenanceService.Refresh(station))
                {
                    hasChanged = true;
                }
            }

            if (hasChanged)
            {
                await _context.SaveChangesAsync();
            }
        }
        private IQueryable<StationSearchRow> BuildStationQuery(List<int> favoriteStationIds, string? status)
        {
            var query = _context.ChargingStations
                .AsNoTracking()
                .Where(x =>
                    x.SystemStatus == StationSystemStatus.Approved &&
                    x.IsVisible &&
                    (x.MaintenanceFeeStatus == StationMaintenanceStatus.Paid ||
                     x.MaintenanceFeeStatus == StationMaintenanceStatus.Active ||
                     x.MaintenanceFeeStatus == StationMaintenanceStatus.ExpiringSoon) &&
                    (x.OperationStatus == StationOperationStatus.Active ||
                     x.Status == ChargingStatus.Active || x.Status == "Hoạt động"))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = ChargingStatus.NormalizeNodeStatus(status);
                query = query.Where(x => x.Status == normalizedStatus || x.Status == status || x.Status == "Hoạt động");
            }

            return query.Select(x => new StationSearchRow
            {
                Id = x.Id,
                Name = x.Name,
                Address = x.Address,
                Status = StationOperationStatus.ToDisplay(x.OperationStatus),
                ChargerType = x.ChargerType,
                Power = x.Power,
                PricePerKwh = x.PricePerKwh,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                AverageRating = x.Reviews.Any() ? Math.Round(x.Reviews.Average(r => (double)r.Rating), 1) : 0,
                ReviewCount = x.Reviews.Count(),
                TotalPoleCount = x.ChargingPoles.Count(),
                ActivePoleCount = x.ChargingPoles.Count(p => p.Status == ChargingStatus.Active || p.Status == "Còn sẵn"),
                AvailablePoleCount = x.ChargingPoles.Count(p => p.Status == ChargingStatus.Active || p.Status == "Còn sẵn"),
                IsFavorite = favoriteStationIds.Contains(x.Id),
                IsVisible = x.IsVisible,
                MaintenanceFeeStatus = x.MaintenanceFeeStatus,
                MaintenanceFeeStatusText = StationMaintenanceStatus.ToDisplay(x.MaintenanceFeeStatus)
            });
        }

        private static StationListItemViewModel MapToListItem(StationSearchRow row, int keywordScore, double? distanceKm)
        {
            return new StationListItemViewModel
            {
                Id = row.Id,
                Name = row.Name,
                Address = row.Address,
                Status = ChargingStatus.NormalizeNodeStatus(row.Status),
                ChargerType = row.ChargerType,
                Power = row.Power,
                PricePerKwh = row.PricePerKwh,
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                AverageRating = row.AverageRating,
                ReviewCount = row.ReviewCount,
                TotalPoleCount = row.TotalPoleCount,
                ActivePoleCount = row.ActivePoleCount,
                AvailablePoleCount = row.AvailablePoleCount,
                IsFavorite = row.IsFavorite,
                SearchScore = keywordScore,
                DistanceKm = distanceKm,
                MaintenanceFeeStatus = row.MaintenanceFeeStatus,
                MaintenanceFeeStatusText = row.MaintenanceFeeStatusText,
                IsVisible = row.IsVisible
            };
        }

        private static bool MatchesKeyword(string? keyword, int score)
        {
            return string.IsNullOrWhiteSpace(keyword) || score > 0;
        }

        private static bool MatchesLocation(string? location, StationSearchRow row)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return true;
            }

            return ContainsNormalized(row.Address, location)
                || ContainsNormalized(row.Name, location);
        }

        private static int CalculateKeywordScore(string? keyword, StationSearchRow row)
        {
            keyword = NormalizeText(keyword);
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return 0;
            }

            var name = NormalizeText(row.Name);
            var address = NormalizeText(row.Address);
            var chargerType = NormalizeText(row.ChargerType);
            var power = NormalizeText(row.Power);
            var status = NormalizeText(row.Status);

            var searchable = string.Join(' ', new[] { name, address, chargerType, power, status }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var score = 0;

            if (name == keyword) score += 180;
            else if (name.StartsWith(keyword, StringComparison.Ordinal)) score += 140;
            else if (name.Contains(keyword, StringComparison.Ordinal)) score += 110;

            if (address.StartsWith(keyword, StringComparison.Ordinal)) score += 90;
            else if (address.Contains(keyword, StringComparison.Ordinal)) score += 70;

            if (chargerType.Contains(keyword, StringComparison.Ordinal)) score += 55;
            if (power.Contains(keyword, StringComparison.Ordinal)) score += 45;
            if (status.Contains(keyword, StringComparison.Ordinal)) score += 25;

            // Why changed: token-based scoring lets users type partial phrases like district + charger type.
            var tokens = keyword
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToArray();

            if (tokens.Length > 0)
            {
                var matchedTokens = tokens.Count(token => searchable.Contains(token, StringComparison.Ordinal));

                if (matchedTokens == tokens.Length)
                {
                    score += 60 + (tokens.Length * 8);
                }
                else
                {
                    score += matchedTokens * 10;
                }
            }

            if (row.IsFavorite)
            {
                score += 4;
            }

            if (row.Status == ChargingStatus.Active || row.Status == "Hoạt động")
            {
                score += 2;
            }

            return score;
        }

        private static string BuildSuggestionSubtitle(StationSearchRow row)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(row.ChargerType))
            {
                parts.Add(row.ChargerType!);
            }

            if (!string.IsNullOrWhiteSpace(row.Power))
            {
                parts.Add(row.Power!);
            }

            if (row.AvailablePoleCount > 0)
            {
                parts.Add($"{row.AvailablePoleCount} trụ đang hoạt động");
            }

            return string.Join(" • ", parts);
        }

        private static string BuildSuggestionBadge(StationSearchRow row)
        {
            if (row.Status == ChargingStatus.Active || row.Status == "Hoạt động")
            {
                return "Đang hoạt động";
            }

            return string.IsNullOrWhiteSpace(row.Status) ? "Trạm sạc" : row.Status!;
        }

        private static bool ContainsNormalized(string? source, string? keyword)
        {
            var normalizedSource = NormalizeText(source);
            var normalizedKeyword = NormalizeText(keyword);

            return !string.IsNullOrWhiteSpace(normalizedKeyword)
                && normalizedSource.Contains(normalizedKeyword, StringComparison.Ordinal);
        }

        private static string NormalizeText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var formD = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(formD.Length);

            foreach (var ch in formD)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(ch switch
                {
                    'đ' => 'd',
                    _ => char.IsWhiteSpace(ch) ? ' ' : ch
                });
            }

            return string.Join(' ', builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private async Task<List<int>> GetFavoriteStationIdsSafeAsync(int? currentUserId)
        {
            if (currentUserId == null)
            {
                return new List<int>();
            }

            if (!await FavoriteTableExistsAsync())
            {
                return new List<int>();
            }

            return await _context.FavoriteStations
                .Where(x => x.UserId == currentUserId.Value)
                .Select(x => x.StationId)
                .ToListAsync();
        }

        // Why changed: prevent logged-in users from crashing the station API when the favorite table is not ready yet.
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

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private static double DegreesToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }

        private sealed class StationSearchRow
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Address { get; set; }
            public string? Status { get; set; }
            public string? ChargerType { get; set; }
            public string? Power { get; set; }
            public decimal PricePerKwh { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double AverageRating { get; set; }
            public int ReviewCount { get; set; }
            public int TotalPoleCount { get; set; }
            public int ActivePoleCount { get; set; }
            public int AvailablePoleCount { get; set; }
            public bool IsFavorite { get; set; }
            public bool IsVisible { get; set; }
            public string? MaintenanceFeeStatus { get; set; }
            public string? MaintenanceFeeStatusText { get; set; }
        }
    }
}
