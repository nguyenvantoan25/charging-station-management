namespace tramsac99.Areas.Admin.Models
{
    public static class StationWorkflowStatus
    {
        public const string Pending = "Chờ duyệt";
        public const string Approved = "Đã duyệt";
        public const string Rejected = "Từ chối";
        public const string AwaitingPayment = "Chờ thanh toán";
        public const string Paid = "Đã thanh toán";
        public const string Completed = "Hoàn tất";
    }
}
