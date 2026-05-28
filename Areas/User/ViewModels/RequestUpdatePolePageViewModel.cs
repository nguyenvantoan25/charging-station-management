using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class RequestUpdatePolePageViewModel : UpdatePoleRequestViewModel
    {
        public string StationName { get; set; } = string.Empty;
        public string StationAddress { get; set; } = string.Empty;
        public string StationStatus { get; set; } = ChargingStatus.Active;
        public string CurrentPoleStatus { get; set; } = ChargingStatus.Active;
        public string? CurrentPolePower { get; set; }
        public string? CurrentPoleNote { get; set; }
        public int HistoryPage { get; set; } = 1;
        public int PolePage { get; set; } = 1;
    }
}