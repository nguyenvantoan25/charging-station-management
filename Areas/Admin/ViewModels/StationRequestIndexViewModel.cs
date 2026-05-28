using tramsac99.Areas.Admin.Models;

namespace tramsac99.Areas.Admin.ViewModels
{
    public class StationRequestIndexViewModel
    {
        public List<StationRegistrationRequest> RegistrationItems { get; set; } = new();

        public int RegistrationPage { get; set; } = 1;
        public int RegistrationPageSize { get; set; } = 8;
        public int RegistrationTotalCount { get; set; }
        public int RegistrationTotalPages => Math.Max(1, (int)Math.Ceiling(RegistrationTotalCount / (double)Math.Max(1, RegistrationPageSize)));

        public int PendingRegistrations { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int AwaitingPaymentCount { get; set; }
    }
}
