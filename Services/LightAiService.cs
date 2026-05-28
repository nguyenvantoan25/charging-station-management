using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace tramsac99.Services
{
    public class LightAiService
    {
        private static readonly Dictionary<string, string[]> TicketCategoryKeywords = new()
        {
            ["Thanh toán"] = new[] { "thanh toan", "payment", "zalopay", "momo", "visa", "master", "hoa don", "invoice", "tru tien", "mat tien", "hoan tien", "refund" },
            ["Tài khoản"] = new[] { "dang nhap", "login", "mat khau", "password", "otp", "tai khoan", "account", "khoa tai khoan" },
            ["Kỹ thuật"] = new[] { "loi", "error", "bug", "khong vao", "treo", "chap chon", "ung dung", "khong hien thi", "khong tim thay" },
            ["Trạm sạc"] = new[] { "tram sac", "tru sac", "cong sac", "khong sac duoc", "sac cham", "khong hoat dong", "bao tri", "charger", "dc", "ac" },
            ["Giá & ưu đãi"] = new[] { "gia", "khuyen mai", "voucher", "coupon", "uu dai", "discount" },
            ["Hợp tác"] = new[] { "hop tac", "franchise", "b2b", "nhuong quyen", "doi tac", "partnership" },
            ["Góp ý"] = new[] { "gop y", "feedback", "de xuat", "suggestion", "kien nghi" }
        };

        private static readonly string[] HighPriorityKeywords =
        {
            "khan", "gap", "ngay lap tuc", "khong the", "khong sac duoc", "tru tien", "mat tien", "loi he thong", "khong dang nhap duoc"
        };

        private static readonly Dictionary<string, string[]> PositiveReviewSignals = new()
        {
            ["dễ tìm"] = new[] { "de tim", "de thay", "de di" },
            ["sạc nhanh"] = new[] { "sac nhanh", "nhanh", "toc do tot" },
            ["ổn định"] = new[] { "on dinh", "it loi", "tot", "ok", "mượt" },
            ["hỗ trợ tốt"] = new[] { "ho tro", "nhan vien", "bao ve", "tu van" },
            ["chỗ đậu thoải mái"] = new[] { "rong", "de dau", "de quay dau", "cho dau xe" }
        };

        private static readonly Dictionary<string, string[]> NegativeReviewSignals = new()
        {
            ["hay đông"] = new[] { "dong", "cho lau", "xep hang" },
            ["khó tìm"] = new[] { "kho tim", "kho thay", "vong" },
            ["có lúc lỗi hoặc bảo trì"] = new[] { "loi", "hong", "bao tri", "mat ket noi" },
            ["giá hơi cao"] = new[] { "gia cao", "dat" },
            ["ít trụ hoạt động"] = new[] { "it tru", "het tru", "khong con tru" }
        };

        private static readonly Dictionary<string, string> KnownLocations = new()
        {
            ["ha noi"] = "Hà Nội",
            ["ho chi minh"] = "Hồ Chí Minh",
            ["sai gon"] = "Hồ Chí Minh",
            ["da nang"] = "Đà Nẵng",
            ["hai phong"] = "Hải Phòng",
            ["bac ninh"] = "Bắc Ninh",
            ["quang ninh"] = "Quảng Ninh",
            ["hai duong"] = "Hải Dương",
            ["vinh phuc"] = "Vĩnh Phúc",
            ["hung yen"] = "Hưng Yên",
            ["can tho"] = "Cần Thơ",
            ["dong nai"] = "Đồng Nai",
            ["binh duong"] = "Bình Dương",
            ["khanh hoa"] = "Khánh Hòa",
            ["nha trang"] = "Nha Trang"
        };

        public SmartStationIntent ParseStationQuery(string? rawQuery)
        {
            var result = new SmartStationIntent
            {
                OriginalQuery = rawQuery?.Trim() ?? string.Empty,
                Keyword = rawQuery?.Trim() ?? string.Empty,
                SortBy = "name"
            };

            if (string.IsNullOrWhiteSpace(rawQuery))
            {
                result.Note = "Nhập tên trạm, khu vực, loại sạc hoặc nhu cầu để tìm nhanh hơn.";
                return result;
            }

            var normalized = Normalize(rawQuery);

            if (normalized.Contains("gan toi") || normalized.Contains("near me"))
            {
                result.UseNearby = true;
                result.SortBy = "nearest";
            }

            if (normalized.Contains("hoat dong"))
            {
                result.Status = "Đang hoạt động";
            }
            else if (normalized.Contains("khong hoat dong"))
            {
                result.Status = "Không hoạt động";
            }

            if (normalized.Contains("dc") || normalized.Contains("nhanh") || normalized.Contains("fast"))
            {
                result.ChargerHint = "DC";
            }
            else if (normalized.Contains("ac") || normalized.Contains("thuong") || normalized.Contains("cham"))
            {
                result.ChargerHint = "AC";
            }

            if (normalized.Contains("tot nhat") || normalized.Contains("danh gia cao") || normalized.Contains("uy tin"))
            {
                result.SortBy = "rating";
            }
            else if (normalized.Contains("con cho") || normalized.Contains("it dong") || normalized.Contains("trong"))
            {
                result.SortBy = "available";
            }

            foreach (var item in KnownLocations)
            {
                if (normalized.Contains(item.Key))
                {
                    result.Location = item.Value;
                    break;
                }
            }

            var removableTokens = new[]
            {
                "gan toi", "near me", "hoat dong", "khong hoat dong", "dc", "ac", "nhanh", "fast", "thuong", "cham",
                "tot nhat", "danh gia cao", "uy tin", "con cho", "it dong", "trong"
            };

            var cleanedKeyword = normalized;
            foreach (var token in removableTokens)
            {
                cleanedKeyword = cleanedKeyword.Replace(token, " ", StringComparison.OrdinalIgnoreCase);
            }

            foreach (var item in KnownLocations.Keys)
            {
                cleanedKeyword = cleanedKeyword.Replace(item, " ", StringComparison.OrdinalIgnoreCase);
            }

            cleanedKeyword = Regex.Replace(cleanedKeyword, "\\s+", " ").Trim();
            result.Keyword = cleanedKeyword;

            result.Note = BuildIntentNote(result);
            return result;
        }

        public TicketAiResult ClassifySupportTicket(string? subject, string? message)
        {
            var merged = Normalize($"{subject} {message}");
            var result = new TicketAiResult
            {
                Category = "Khác",
                Priority = "Trung bình",
                Summary = "Yêu cầu hỗ trợ chung cần được tiếp nhận và kiểm tra thêm."
            };

            var bestScore = 0;
            foreach (var item in TicketCategoryKeywords)
            {
                var score = item.Value.Count(keyword => merged.Contains(keyword));
                if (score > bestScore)
                {
                    bestScore = score;
                    result.Category = item.Key;
                }
            }

            if (HighPriorityKeywords.Any(keyword => merged.Contains(keyword)) ||
                merged.Contains("khong sac") ||
                merged.Contains("tru tien") ||
                merged.Contains("mat tien") ||
                merged.Contains("loi he thong"))
            {
                result.Priority = "Cao";
            }
            else if (result.Category is "Kỹ thuật" or "Thanh toán" or "Tài khoản" or "Trạm sạc")
            {
                result.Priority = "Trung bình";
            }
            else
            {
                result.Priority = "Thấp";
            }

            result.Summary = result.Category switch
            {
                "Thanh toán" => "Ticket liên quan đến giao dịch hoặc đối soát thanh toán.",
                "Tài khoản" => "Ticket liên quan đến đăng nhập, bảo mật hoặc quyền truy cập tài khoản.",
                "Kỹ thuật" => "Ticket phản ánh lỗi hiển thị, lỗi hệ thống hoặc hành vi bất thường của ứng dụng.",
                "Trạm sạc" => "Ticket liên quan đến khả năng hoạt động hoặc tình trạng trạm/trụ sạc.",
                "Giá & ưu đãi" => "Ticket liên quan đến bảng giá, voucher hoặc chính sách ưu đãi.",
                "Hợp tác" => "Ticket thiên về nhu cầu hợp tác, đối tác hoặc mở rộng kinh doanh.",
                "Góp ý" => "Ticket mang tính góp ý, đề xuất cải thiện trải nghiệm dịch vụ.",
                _ => "Yêu cầu hỗ trợ chung cần được tiếp nhận và phân loại thêm."
            };

            return result;
        }

        public string BuildReviewSummary(double averageRating, int reviewCount, IEnumerable<string?> comments)
        {
            if (reviewCount <= 0)
            {
                return "Chưa có đánh giá. Bạn có thể xem chi tiết trạm để là người đầu tiên nhận xét.";
            }

            var ratingText = averageRating switch
            {
                >= 4.5 => "Được đánh giá rất tốt",
                >= 4.0 => "Được đánh giá khá tốt",
                >= 3.5 => "Trải nghiệm nhìn chung ổn định",
                >= 3.0 => "Chất lượng ở mức trung bình",
                _ => "Cần xem kỹ phản hồi trước khi ghé"
            };

            var pros = BuildReviewHighlights(comments, positive: true, take: 2);
            var cons = BuildReviewHighlights(comments, positive: false, take: 1);

            var summary = new StringBuilder();
            summary.Append($"{ratingText} ({averageRating:0.0}/5 từ {reviewCount} đánh giá)");

            if (pros.Any())
            {
                summary.Append($". Người dùng hay nhắc: {string.Join(", ", pros)}");
            }

            if (cons.Any())
            {
                summary.Append($". Cần lưu ý: {string.Join(", ", cons)}");
            }

            return summary.ToString();
        }

        public RecommendationProfile BuildRecommendationProfile(IEnumerable<StationProfile> sourceStations)
        {
            var stations = sourceStations.ToList();

            return new RecommendationProfile
            {
                PreferredChargerTypes = stations
                    .Where(x => !string.IsNullOrWhiteSpace(x.ChargerType))
                    .GroupBy(x => Normalize(x.ChargerType))
                    .OrderByDescending(x => x.Count())
                    .Take(3)
                    .Select(x => x.Key)
                    .ToList(),

                PreferredPowerBuckets = stations
                    .Select(x => ExtractPowerBucket(x.Power))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .GroupBy(x => x)
                    .OrderByDescending(x => x.Count())
                    .Take(3)
                    .Select(x => x.Key)
                    .ToList(),

                PreferredLocationTokens = stations
                    .SelectMany(x => ExtractAddressTokens(x.Address))
                    .GroupBy(x => x)
                    .OrderByDescending(x => x.Count())
                    .Take(5)
                    .Select(x => x.Key)
                    .ToList()
            };
        }

        public double CalculateRecommendationScore(RecommendationProfile profile, StationCandidate candidate, double? distanceKm = null)
        {
            var score = 0d;

            if (string.Equals(candidate.Status, "Đang hoạt động", StringComparison.OrdinalIgnoreCase))
            {
                score += 18;
            }

            score += Math.Min(candidate.AverageRating, 5) * 7;
            score += Math.Min(candidate.AvailablePoleCount, 6) * 4;

            if (profile.PreferredChargerTypes.Contains(Normalize(candidate.ChargerType)))
            {
                score += 28;
            }

            if (profile.PreferredPowerBuckets.Contains(ExtractPowerBucket(candidate.Power)))
            {
                score += 14;
            }

            var candidateAddressTokens = ExtractAddressTokens(candidate.Address);
            if (candidateAddressTokens.Any(token => profile.PreferredLocationTokens.Contains(token)))
            {
                score += 20;
            }

            if (distanceKm.HasValue)
            {
                score += Math.Max(0, 18 - Math.Min(distanceKm.Value, 18));
            }

            return Math.Round(score, 2);
        }

        public string BuildRecommendationReason(RecommendationProfile profile, StationCandidate candidate, double? distanceKm = null)
        {
            if (distanceKm.HasValue && distanceKm.Value <= 5)
            {
                return "Ưu tiên vì đang ở gần vị trí của bạn.";
            }

            if (profile.PreferredChargerTypes.Contains(Normalize(candidate.ChargerType)))
            {
                return "Phù hợp với loại sạc bạn thường quan tâm.";
            }

            if (profile.PreferredPowerBuckets.Contains(ExtractPowerBucket(candidate.Power)))
            {
                return "Có mức công suất gần với các trạm bạn từng xem.";
            }

            if (ExtractAddressTokens(candidate.Address).Any(token => profile.PreferredLocationTokens.Contains(token)))
            {
                return "Nằm trong khu vực bạn thường tra cứu hoặc lưu ý.";
            }

            if (candidate.AvailablePoleCount >= 3)
            {
                return "Đang có số lượng trụ hoạt động khá tốt.";
            }

            return "Được đề xuất vì điểm đánh giá và trạng thái vận hành tốt.";
        }

        public string BuildSearchReason(SmartStationIntent intent, StationCandidate candidate)
        {
            var reasons = new List<string>();

            if (!string.IsNullOrWhiteSpace(intent.ChargerHint) && Normalize(candidate.ChargerType).Contains(Normalize(intent.ChargerHint)))
            {
                reasons.Add($"khớp loại sạc {intent.ChargerHint}");
            }

            if (!string.IsNullOrWhiteSpace(intent.Location) && Normalize(candidate.Address).Contains(Normalize(intent.Location)))
            {
                reasons.Add($"thuộc khu vực {intent.Location}");
            }

            if (!string.IsNullOrWhiteSpace(intent.Status) && string.Equals(candidate.Status, intent.Status, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("đúng trạng thái bạn cần");
            }

            if (candidate.AvailablePoleCount > 0)
            {
                reasons.Add("đang còn trụ hoạt động");
            }

            if (!reasons.Any())
            {
                return "Được xếp lên trước nhờ mức đánh giá và độ sẵn sàng hiện tại.";
            }

            return "Phù hợp vì " + string.Join(", ", reasons) + ".";
        }

        public IReadOnlyList<string> BuildReviewHighlights(IEnumerable<string?> comments, bool positive, int take = 3)
        {
            var source = positive ? PositiveReviewSignals : NegativeReviewSignals;
            var merged = Normalize(string.Join(" | ", comments.Where(x => !string.IsNullOrWhiteSpace(x))));
            if (string.IsNullOrWhiteSpace(merged))
            {
                return Array.Empty<string>();
            }

            return source
                .Select(item => new
                {
                    Label = item.Key,
                    Score = item.Value.Count(keyword => merged.Contains(keyword))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(take)
                .Select(x => x.Label)
                .ToList();
        }

        public List<string> ExtractAddressTokens(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new List<string>();
            }

            return Normalize(address)
                .Split(new[] { ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length >= 3)
                .TakeLast(3)
                .ToList();
        }

        private static string BuildIntentNote(SmartStationIntent intent)
        {
            var notes = new List<string>();

            if (!string.IsNullOrWhiteSpace(intent.Status))
            {
                notes.Add($"lọc theo trạng thái {intent.Status.ToLowerInvariant()}");
            }

            if (!string.IsNullOrWhiteSpace(intent.Location))
            {
                notes.Add($"ưu tiên khu vực {intent.Location}");
            }

            if (!string.IsNullOrWhiteSpace(intent.ChargerHint))
            {
                notes.Add($"ưu tiên loại sạc {intent.ChargerHint}");
            }

            if (intent.UseNearby)
            {
                notes.Add("ưu tiên khoảng cách gần bạn");
            }

            if (!notes.Any())
            {
                return "Đã giữ nguyên từ khóa và tìm theo dữ liệu hiện có trong hệ thống.";
            }

            return "Tìm thông minh đã hiểu: " + string.Join(", ", notes) + ".";
        }

        private static string ExtractPowerBucket(string? power)
        {
            if (string.IsNullOrWhiteSpace(power))
            {
                return string.Empty;
            }

            var match = Regex.Match(power, "\\d+");
            if (!match.Success)
            {
                return Normalize(power);
            }

            var kw = int.Parse(match.Value);
            return kw switch
            {
                <= 22 => "low",
                <= 60 => "medium",
                _ => "high"
            };
        }

        private static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var formD = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var ch in formD)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder
                .ToString()
                .Replace('đ', 'd')
                .Normalize(NormalizationForm.FormC);
        }
    }

    public class SmartStationIntent
    {
        public string OriginalQuery { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Status { get; set; }
        public string? ChargerHint { get; set; }
        public string SortBy { get; set; } = "name";
        public bool UseNearby { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class TicketAiResult
    {
        public string Category { get; set; } = "Khác";
        public string Priority { get; set; } = "Trung bình";
        public string Summary { get; set; } = string.Empty;
    }

    public class StationProfile
    {
        public int Id { get; set; }
        public string? Address { get; set; }
        public string? ChargerType { get; set; }
        public string? Power { get; set; }
    }

    public class StationCandidate
    {
        public int Id { get; set; }
        public string? Address { get; set; }
        public string? Status { get; set; }
        public string? ChargerType { get; set; }
        public string? Power { get; set; }
        public double AverageRating { get; set; }
        public int AvailablePoleCount { get; set; }
    }

    public class RecommendationProfile
    {
        public List<string> PreferredChargerTypes { get; set; } = new();
        public List<string> PreferredPowerBuckets { get; set; } = new();
        public List<string> PreferredLocationTokens { get; set; } = new();
    }
}
