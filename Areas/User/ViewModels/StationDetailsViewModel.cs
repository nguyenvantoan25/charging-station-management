namespace tramsac99.Areas.User.ViewModels
{
    public class StationDetailsViewModel
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
        public int AvailablePoleCount { get; set; }
        public bool IsFavorite { get; set; }
        public bool HasReviewed { get; set; }
        public int? CurrentUserRating { get; set; }
        public string? CurrentUserComment { get; set; }
        public List<PoleItemViewModel> Poles { get; set; } = new();
        public List<ReviewItemViewModel> Reviews { get; set; } = new();
        public string GoogleMapUrl => $"https://www.google.com/maps?q={Latitude},{Longitude}";
    }
}
