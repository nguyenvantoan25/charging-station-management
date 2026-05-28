using tramsac99.Areas.Admin.Models.Dto;

namespace tramsac99.Areas.Admin.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalStations { get; set; }
        public int AdminManagedStations { get; set; }
        public int UserSubmittedStations { get; set; }
        public int TotalReviews { get; set; }
        public int TotalPoles { get; set; }
        public int ActivePoles { get; set; }
        public List<ChargingStationDto> Stations { get; set; } = new();
    }
}
