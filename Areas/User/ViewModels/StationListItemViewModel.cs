namespace tramsac99.Areas.User.ViewModels
{
    public class StationListItemViewModel
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
        public int SearchScore { get; set; }
        public double? DistanceKm { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsVisible { get; set; }
        public string? MaintenanceFeeStatus { get; set; }
        public string? MaintenanceFeeStatusText { get; set; }
    }
}
