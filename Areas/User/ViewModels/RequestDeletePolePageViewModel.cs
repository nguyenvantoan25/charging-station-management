using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.User.ViewModels
{
    public class RequestDeletePolePageViewModel : DeletePoleRequestViewModel
    {
        public string StationName { get; set; } = string.Empty;
        public string PoleCode { get; set; } = string.Empty;
        public string PoleStatus { get; set; } = ChargingStatus.Active;
        public string? PoleMaxPower { get; set; }
        public string? PoleNote { get; set; }
        public int HistoryPage { get; set; } = 1;
        public int PolePage { get; set; } = 1;
    }
}