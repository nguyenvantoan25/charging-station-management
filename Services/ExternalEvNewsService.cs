using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using tramsac99.Areas.User.ViewModels;

namespace tramsac99.Services
{
    public class ExternalEvNewsService
    {
        private readonly HttpClient _httpClient;

        private static readonly (string Source, string FeedUrl)[] FeedSources =
        {
            // Why changed: use public RSS feeds so the news page can pull EV articles from external sites.
            ("Electrek", "https://electrek.co/feed/"),
            ("InsideEVs", "https://insideevs.com/rss/news/all/")
        };

        public ExternalEvNewsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(15);

            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 TramSac99NewsBot/1.0");
            }
        }

        public async Task<List<ExternalEvNewsArticleViewModel>> GetLatestAsync(int take = 60)
        {
            var allItems = new List<ExternalEvNewsArticleViewModel>();

            foreach (var source in FeedSources)
            {
                var sourceItems = await ReadFeedAsync(source.Source, source.FeedUrl);
                allItems.AddRange(sourceItems);
            }

            return allItems
                .Where(x => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Link))
                .GroupBy(x => x.Link.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderByDescending(x => x.PublishedAt ?? DateTime.MinValue)
                .Take(take)
                .ToList();
        }

        private async Task<List<ExternalEvNewsArticleViewModel>> ReadFeedAsync(string sourceName, string feedUrl)
        {
            try
            {
                var xmlContent = await _httpClient.GetStringAsync(feedUrl);
                var document = XDocument.Parse(xmlContent);

                var rssItems = document.Descendants("item")
                    .Select(item =>
                    {
                        var title = CleanText(item.Element("title")?.Value, 300);
                        var description = item.Element("description")?.Value;
                        var encodedContent = item.Elements().FirstOrDefault(x => x.Name.LocalName == "encoded")?.Value;
                        var imageUrl = ExtractImageUrl(item, description, encodedContent);

                        return new ExternalEvNewsArticleViewModel
                        {
                            Source = sourceName,
                            Title = title,
                            Summary = CleanText(description, 500),
                            Link = (item.Element("link")?.Value ?? string.Empty).Trim(),
                            PublishedAt = ParseDate(item.Element("pubDate")?.Value),
                            ImageUrl = imageUrl
                        };
                    })
                    .ToList();

                if (rssItems.Any())
                {
                    return rssItems;
                }

                XNamespace atom = "http://www.w3.org/2005/Atom";

                var atomItems = document.Descendants(atom + "entry")
                    .Select(entry =>
                    {
                        var summary = entry.Element(atom + "summary")?.Value ?? entry.Element(atom + "content")?.Value;
                        var imageUrl = ExtractImageUrl(entry, summary, summary);

                        return new ExternalEvNewsArticleViewModel
                        {
                            Source = sourceName,
                            Title = CleanText(entry.Element(atom + "title")?.Value, 300),
                            Summary = CleanText(summary, 500),
                            Link = (entry.Elements(atom + "link").FirstOrDefault()?.Attribute("href")?.Value ?? string.Empty).Trim(),
                            PublishedAt = ParseDate(entry.Element(atom + "updated")?.Value ?? entry.Element(atom + "published")?.Value),
                            ImageUrl = imageUrl
                        };
                    })
                    .ToList();

                return atomItems;
            }
            catch
            {
                // Why changed: if one feed fails, the page should still work with the remaining feeds.
                return new List<ExternalEvNewsArticleViewModel>();
            }
        }

        private static string? ExtractImageUrl(XElement item, params string?[] htmlCandidates)
        {
            var enclosureUrl = item.Elements()
                .FirstOrDefault(x => x.Name.LocalName.Equals("enclosure", StringComparison.OrdinalIgnoreCase))
                ?.Attribute("url")?.Value;

            if (IsValidImageUrl(enclosureUrl))
            {
                return enclosureUrl;
            }

            var mediaUrl = item.Descendants()
                .Where(x => x.Name.LocalName.Equals("content", StringComparison.OrdinalIgnoreCase)
                         || x.Name.LocalName.Equals("thumbnail", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Attribute("url")?.Value)
                .FirstOrDefault(IsValidImageUrl);

            if (IsValidImageUrl(mediaUrl))
            {
                return mediaUrl;
            }

            foreach (var html in htmlCandidates)
            {
                var imageUrl = ExtractFirstImageFromHtml(html);
                if (IsValidImageUrl(imageUrl))
                {
                    return imageUrl;
                }
            }

            return null;
        }

        private static string? ExtractFirstImageFromHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var match = Regex.Match(
                html,
                @"<img[^>]+src\s*=\s*[""'](?<src>[^""']+)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return match.Success ? match.Groups["src"].Value.Trim() : null;
        }

        private static bool IsValidImageUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out _);
        }

        private static DateTime? ParseDate(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            return DateTime.TryParse(rawValue, out var parsed)
                ? parsed
                : null;
        }

        private static string CleanText(string? rawValue, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            var decoded = WebUtility.HtmlDecode(rawValue);
            var noHtml = Regex.Replace(decoded, "<.*?>", " ");
            var normalized = Regex.Replace(noHtml, @"\s+", " ").Trim();

            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength).Trim() + "…";
        }
    }
}
