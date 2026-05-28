namespace tramsac99.Areas.User.ViewModels
{
    public class ExternalEvNewsArticleViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? ImageUrl { get; set; } // Why changed: every news box needs a thumbnail image.
        public DateTime? PublishedAt { get; set; }
    }
}
