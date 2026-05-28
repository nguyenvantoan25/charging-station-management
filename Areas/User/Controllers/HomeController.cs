using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
namespace tramsac99.Areas.User.Controllers
{
    [Area("User")]
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        // Why changed: keep only ONE constructor so ASP.NET Core DI can resolve HomeController correctly.
        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Route()
        {
            return View();
        }

        public IActionResult RouteHistory()
        {
            return RedirectToAction(nameof(ActivityHistory));
        }

        [Authorize]
        public IActionResult ActivityHistory()
        {
            return View();
        }

        // Why changed: keep contact page route for the user navbar.
        public IActionResult Contact()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> PlaceSuggest([FromQuery] string keyword)
        {
            keyword = (keyword ?? string.Empty).Trim();

            if (keyword.Length < 3)
            {
                return Json(Array.Empty<object>());
            }

            var apiKey = _configuration["Map4D:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Json(Array.Empty<object>());
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Why changed: fetch Map4D autosuggest on the server and normalize the payload for the route page.
            var url = $"https://api.map4d.vn/sdk/autosuggest?text={Uri.EscapeDataString(keyword)}&key={Uri.EscapeDataString(apiKey)}";

            using var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return Json(Array.Empty<object>());
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            var suggestions = ExtractSuggestions(document.RootElement)
                .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
                .Where(IsVietnamSuggestion)
                .Take(8)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Address,
                    x.DisplayName,
                    x.Lat,
                    x.Lng
                })
                .ToList();

            return Json(suggestions);
        }

        [HttpGet]
        public async Task<IActionResult> PlaceResolve([FromQuery] string keyword, [FromQuery] string? placeId)
        {
            keyword = (keyword ?? string.Empty).Trim();

            if (keyword.Length < 2)
            {
                return Json(new { success = false, message = "Vui lòng nhập địa điểm thuộc Việt Nam." });
            }

            var apiKey = _configuration["Map4D:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                using var map4dHttp = new HttpClient();
                map4dHttp.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var suggestUrl = $"https://api.map4d.vn/sdk/autosuggest?text={Uri.EscapeDataString(keyword)}&key={Uri.EscapeDataString(apiKey)}";
                using var suggestResponse = await map4dHttp.GetAsync(suggestUrl);

                if (suggestResponse.IsSuccessStatusCode)
                {
                    var json = await suggestResponse.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(json);
                    var item = ExtractSuggestions(document.RootElement)
                        .Where(IsVietnamSuggestion)
                        .FirstOrDefault(x =>
                            string.IsNullOrWhiteSpace(placeId) ||
                            string.Equals(x.Id, placeId, StringComparison.OrdinalIgnoreCase));

                    if (item != null && item.Lat.HasValue && item.Lng.HasValue && IsVietnamCoordinate(item.Lat.Value, item.Lng.Value))
                    {
                        return Json(new
                        {
                            success = true,
                            item = new
                            {
                                item.Id,
                                item.Name,
                                item.Address,
                                item.DisplayName,
                                lat = item.Lat,
                                lng = item.Lng
                            }
                        });
                    }
                }
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TramSac99/1.0");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi");

            var url = $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&countrycodes=vn&q={Uri.EscapeDataString(keyword)}";
            using var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return Json(new { success = false, message = "Không tìm thấy địa điểm trong Việt Nam." });
            }

            var raw = await response.Content.ReadAsStringAsync();
            using var nominatimDocument = JsonDocument.Parse(raw);
            if (nominatimDocument.RootElement.ValueKind != JsonValueKind.Array || nominatimDocument.RootElement.GetArrayLength() == 0)
            {
                return Json(new { success = false, message = "Không tìm thấy địa điểm trong Việt Nam." });
            }

            var first = nominatimDocument.RootElement[0];
            var displayName = FirstString(first, "display_name") ?? keyword;
            var latText = FirstString(first, "lat");
            var lngText = FirstString(first, "lon");

            if (!double.TryParse(latText, out var lat) || !double.TryParse(lngText, out var lng) || !IsVietnamCoordinate(lat, lng))
            {
                return Json(new { success = false, message = "Hệ thống chỉ hỗ trợ lộ trình nằm trong Việt Nam." });
            }

            return Json(new
            {
                success = true,
                item = new
                {
                    id = "",
                    name = keyword,
                    address = displayName,
                    displayName,
                    lat,
                    lng
                }
            });
        }

        private static IEnumerable<SuggestItem> ExtractSuggestions(JsonElement root)
        {
            var items = FindSuggestionArray(root);

            if (items.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var item in items.EnumerateArray())
            {
                var name = FirstString(item, "name", "title", "text");
                var displayName = FirstString(item, "displayName", "display_name", "description");
                var address = FirstString(item, "address", "formattedAddress", "fullAddress", "description");
                var id = FirstString(item, "id", "refId", "ref_id", "placeId", "place_id");

                var (lat, lng) = ExtractLatLng(item);

                var finalName = !string.IsNullOrWhiteSpace(name)
                    ? name
                    : (!string.IsNullOrWhiteSpace(displayName) ? displayName : "Địa điểm");

                var finalAddress = !string.IsNullOrWhiteSpace(address)
                    ? address
                    : (!string.IsNullOrWhiteSpace(displayName) ? displayName : finalName);

                var finalDisplayName = !string.IsNullOrWhiteSpace(displayName)
                    ? displayName
                    : finalAddress;

                yield return new SuggestItem
                {
                    Id = id,
                    Name = finalName,
                    Address = finalAddress,
                    DisplayName = finalDisplayName,
                    Lat = lat,
                    Lng = lng
                };
            }
        }

        private static JsonElement FindSuggestionArray(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root;
            }

            var candidates = new[] { "result", "results", "data", "items", "suggestions", "predictions" };

            foreach (var name in candidates)
            {
                if (root.TryGetProperty(name, out var value))
                {
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        return value;
                    }

                    if (value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var nestedName in candidates)
                        {
                            if (value.TryGetProperty(nestedName, out var nestedValue) && nestedValue.ValueKind == JsonValueKind.Array)
                            {
                                return nestedValue;
                            }
                        }
                    }
                }
            }

            return root;
        }

        private static string? FirstString(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }

            return null;
        }

        private static (double? lat, double? lng) ExtractLatLng(JsonElement element)
        {
            double? lat = null;
            double? lng = null;

            if (TryGetDouble(element, "lat", out var directLat)) lat = directLat;
            if (TryGetDouble(element, "lng", out var directLng) || TryGetDouble(element, "lon", out directLng) || TryGetDouble(element, "longitude", out directLng)) lng = directLng;

            if (lat.HasValue && lng.HasValue)
            {
                return (lat, lng);
            }

            var nestedNames = new[] { "location", "coordinate", "coordinates", "geometry", "center" };
            foreach (var nestedName in nestedNames)
            {
                if (!element.TryGetProperty(nestedName, out var nested) || nested.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!lat.HasValue && (TryGetDouble(nested, "lat", out var nestedLat) || TryGetDouble(nested, "latitude", out nestedLat)))
                {
                    lat = nestedLat;
                }

                if (!lng.HasValue && (TryGetDouble(nested, "lng", out var nestedLng) || TryGetDouble(nested, "lon", out nestedLng) || TryGetDouble(nested, "longitude", out nestedLng)))
                {
                    lng = nestedLng;
                }

                if (lat.HasValue && lng.HasValue)
                {
                    return (lat, lng);
                }
            }

            return (lat, lng);
        }

        private static bool IsVietnamSuggestion(SuggestItem item)
        {
            if (item.Lat.HasValue && item.Lng.HasValue)
            {
                return IsVietnamCoordinate(item.Lat.Value, item.Lng.Value);
            }

            var text = $"{item.DisplayName} {item.Address} {item.Name}".ToLowerInvariant();
            return text.Contains("việt nam") || text.Contains("viet nam") || text.Contains("vietnam");
        }

        private static bool IsVietnamCoordinate(double lat, double lng)
        {
            if (lat < 8.0 || lat > 23.8 || lng < 102.0 || lng > 110.5)
            {
                return false;
            }

            // BBox Việt Nam bao cả một phần Lào/Campuchia. Ràng buộc thêm theo hình chữ S
            // để gợi ý/resolve địa điểm không nhận nhầm điểm ngoài lãnh thổ Việt Nam.
            var minLng = GetVietnamMinLngByLat(lat) - 0.35;
            var maxLng = lat >= 15 ? 109.8 : 107.9;
            return lng >= minLng && lng <= maxLng;
        }

        private static double GetVietnamMinLngByLat(double lat)
        {
            var points = new (double Lat, double Lng)[]
            {
                (23.4, 102.0), (22.2, 102.1), (21.0, 102.4), (20.0, 103.0),
                (19.0, 103.8), (18.0, 104.9), (17.0, 105.7), (16.0, 106.5),
                (15.0, 107.0), (14.0, 107.1), (13.0, 107.0), (12.0, 106.7),
                (11.0, 106.0), (10.0, 104.6), (9.0, 104.55), (8.3, 104.5)
            };

            for (var i = 0; i < points.Length - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                if ((lat <= a.Lat && lat >= b.Lat) || (lat >= a.Lat && lat <= b.Lat))
                {
                    var t = (lat - a.Lat) / ((b.Lat - a.Lat) == 0 ? 1 : (b.Lat - a.Lat));
                    return a.Lng + (b.Lng - a.Lng) * t;
                }
            }

            return lat >= 15 ? 106.5 : 104.5;
        }

        private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
        {
            value = 0;

            if (!element.TryGetProperty(propertyName, out var raw))
            {
                return false;
            }

            if (raw.ValueKind == JsonValueKind.Number)
            {
                return raw.TryGetDouble(out value);
            }

            if (raw.ValueKind == JsonValueKind.String)
            {
                return double.TryParse(raw.GetString(), out value);
            }

            return false;
        }

        private sealed class SuggestItem
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Address { get; set; }
            public string? DisplayName { get; set; }
            public double? Lat { get; set; }
            public double? Lng { get; set; }
        }
    }
}
