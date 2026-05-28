namespace tramsac99.Areas.Admin.ViewModels
{
    public class RevenueStatisticsViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal YesterdayRevenue { get; set; }
        public decimal ThisWeekRevenue { get; set; }
        public decimal LastWeekRevenue { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public decimal RegistrationRevenue { get; set; }
        public decimal MaintenanceRevenue { get; set; }

        public CompareResultViewModel TodayCompare { get; set; } = new();
        public CompareResultViewModel WeekCompare { get; set; } = new();
        public CompareResultViewModel MonthCompare { get; set; } = new();
        public CompareResultViewModel TotalCompare { get; set; } = new();

        public int SuccessTransactionCount { get; set; }
        public int PendingTransactionCount { get; set; }
        public int FailedTransactionCount { get; set; }
        public int ActiveStationCount { get; set; }
        public int UnpaidStationCount { get; set; }
        public int HiddenStationCount { get; set; }

        public List<DailyRevenuePointViewModel> DailyRevenueChartData { get; set; } = new();
        public TodayYesterdayChartViewModel TodayYesterdayChartData { get; set; } = new();
        public WeekCompareChartViewModel WeekCompareChartData { get; set; } = new();
        public List<RevenueByTypePointViewModel> RevenueByTypeChartData { get; set; } = new();
        public List<RecentTransactionViewModel> RecentTransactions { get; set; } = new();

        public RevenueStatisticsFilterViewModel Filter { get; set; } = new();

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalTransactions { get; set; }
        public int TotalPages { get; set; } = 1;
    }

    public class CompareResultViewModel
    {
        public decimal Difference { get; set; }
        public decimal? Percent { get; set; }
        public string Text { get; set; } = "Không thay đổi";
        public string Trend { get; set; } = "same";
        public string IconClass { get; set; } = "fas fa-minus";
    }

    public class DailyRevenuePointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class TodayYesterdayChartViewModel
    {
        public decimal Today { get; set; }
        public decimal Yesterday { get; set; }
        public decimal Difference { get; set; }
        public decimal? Percent { get; set; }
        public string CompareText { get; set; } = "Không thay đổi";
    }

    public class WeekCompareChartViewModel
    {
        public decimal ThisWeekRegistration { get; set; }
        public decimal ThisWeekMaintenance { get; set; }
        public decimal LastWeekRegistration { get; set; }
        public decimal LastWeekMaintenance { get; set; }
    }

    public class RevenueByTypePointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Percent { get; set; }
    }

    public class RecentTransactionViewModel
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string TransactionCode { get; set; } = "-";
        public string OwnerName { get; set; } = "-";
        public string StationName { get; set; } = "-";
        public string PaymentType { get; set; } = "Giao dịch";
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "PayOS";
        public string Status { get; set; } = "-";
        public string StatusKey { get; set; } = "other";
        public DateTime ActivityAt { get; set; }
    }

    public class RevenueStatisticsFilterViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Type { get; set; } = "all";
        public string Status { get; set; } = "all";
        public string OwnerKeyword { get; set; } = string.Empty;
        public string StationKeyword { get; set; } = string.Empty;
    }
}
