using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Areas.Admin.ViewModels;
using tramsac99.Data;

namespace tramsac99.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RevenueStatisticsController : Controller
    {
        private static readonly string[] RegistrationTypes = { PaymentTransactionType.InitialRegistration, PaymentTransactionType.LegacyInitialRegistration, "initial_registration", "registration" };
        private static readonly string[] MaintenanceTypes = { PaymentTransactionType.Maintenance, PaymentTransactionType.LegacyMaintenance, "maintenance" };
        private static readonly string[] SuccessStatuses = { PaymentTransactionStatus.Paid, "Đã thanh toán", "Thanh toán thành công", "success", "Success", "paid", "Paid" };
        private static readonly string[] PendingStatuses = { PaymentTransactionStatus.Pending, "Đang chờ thanh toán", "pending", "Pending" };
        private static readonly string[] FailedStatuses = { PaymentTransactionStatus.Failed, PaymentTransactionStatus.Cancelled, "Thanh toán chưa thành công", "Đã hủy thanh toán", "failed", "Failed", "cancelled", "Cancelled", "expired", "Expired", "Hết hạn" };

        private readonly AppDbContext _context;
        public RevenueStatisticsController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string type = "all", string status = "all", string ownerKeyword = "", string stationKeyword = "", int page = 1, int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);
            type = NormalizeFilter(type);
            status = NormalizeFilter(status);
            ownerKeyword = (ownerKeyword ?? string.Empty).Trim();
            stationKeyword = (stationKeyword ?? string.Empty).Trim();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var yesterday = today.AddDays(-1);
            var dayBeforeYesterday = today.AddDays(-2);
            var thisWeekStart = GetMonday(today);
            var nextWeekStart = thisWeekStart.AddDays(7);
            var lastWeekStart = thisWeekStart.AddDays(-7);
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = thisMonthStart.AddMonths(1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);

            var filteredQuery = ApplyCommonFilters(_context.PaymentTransactions.AsNoTracking(), type, ownerKeyword, stationKeyword, fromDate, toDate, true);
            var noDateQuery = ApplyCommonFilters(_context.PaymentTransactions.AsNoTracking(), type, ownerKeyword, stationKeyword, null, null, false);
            var successFiltered = ApplyStatusFilter(filteredQuery, "success");
            var successNoDate = ApplyStatusFilter(noDateQuery, "success");

            var totalRevenue = await SumAmountAsync(successFiltered);
            var todayRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= today && x.PaidAt < tomorrow));
            var yesterdayRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= yesterday && x.PaidAt < today));
            var dayBeforeYesterdayRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= dayBeforeYesterday && x.PaidAt < yesterday));
            var thisWeekRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= thisWeekStart && x.PaidAt < nextWeekStart));
            var lastWeekRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= lastWeekStart && x.PaidAt < thisWeekStart));
            var thisMonthRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= thisMonthStart && x.PaidAt < nextMonthStart));
            var lastMonthRevenue = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= lastMonthStart && x.PaidAt < thisMonthStart));
            var registrationRevenue = await SumAmountAsync(successFiltered.Where(x => RegistrationTypes.Contains(x.PaymentType)));
            var maintenanceRevenue = await SumAmountAsync(successFiltered.Where(x => MaintenanceTypes.Contains(x.PaymentType)));
            var tableQuery = ApplyStatusFilter(filteredQuery, status)
                .Include(x => x.User)
                .Include(x => x.Station)
                .Include(x => x.RegistrationRequest);

            var successCount = await ApplyStatusFilter(filteredQuery, "success").CountAsync();
            var pendingCount = await ApplyStatusFilter(filteredQuery, "pending").CountAsync();
            var failedCount = await ApplyStatusFilter(filteredQuery, "failed").CountAsync();
            var activeStationCount = await _context.ChargingStations.AsNoTracking().CountAsync(x => x.IsVisible && (x.MaintenanceFeeStatus == StationMaintenanceStatus.Paid || x.MaintenanceFeeStatus == StationMaintenanceStatus.Active || x.MaintenanceFeeStatus == StationMaintenanceStatus.ExpiringSoon));
            var unpaidStationCount = await _context.ChargingStations.AsNoTracking().CountAsync(x => x.MaintenanceFeeStatus == StationMaintenanceStatus.Unpaid || x.MaintenanceFeeStatus == StationMaintenanceStatus.Expired || x.MaintenanceFeeStatus == StationMaintenanceStatus.MaintenanceUnpaid);
            var hiddenStationCount = await _context.ChargingStations.AsNoTracking().CountAsync(x => !x.IsVisible || x.MaintenanceFeeStatus == StationMaintenanceStatus.Hidden || x.MaintenanceFeeStatus == StationMaintenanceStatus.Locked || x.SystemStatus == StationSystemStatus.Locked);

            var totalTransactions = await tableQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalTransactions / (double)pageSize));
            page = Math.Min(page, totalPages);
            var recentEntities = await tableQuery.OrderByDescending(x => x.PaidAt ?? x.CancelledAt ?? x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dailyRevenue = new List<DailyRevenuePointViewModel>();
            for (var i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var nextDate = date.AddDays(1);
                dailyRevenue.Add(new DailyRevenuePointViewModel
                {
                    Label = date.ToString("dd/MM"),
                    Amount = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= date && x.PaidAt < nextDate))
                });
            }

            var thisWeekRegistration = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= thisWeekStart && x.PaidAt < nextWeekStart && RegistrationTypes.Contains(x.PaymentType)));
            var thisWeekMaintenance = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= thisWeekStart && x.PaidAt < nextWeekStart && MaintenanceTypes.Contains(x.PaymentType)));
            var lastWeekRegistration = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= lastWeekStart && x.PaidAt < thisWeekStart && RegistrationTypes.Contains(x.PaymentType)));
            var lastWeekMaintenance = await SumAmountAsync(successNoDate.Where(x => x.PaidAt >= lastWeekStart && x.PaidAt < thisWeekStart && MaintenanceTypes.Contains(x.PaymentType)));

            var totalByType = registrationRevenue + maintenanceRevenue;
            var revenueByType = new List<RevenueByTypePointViewModel>
            {
                new() { Label = "Phí đăng ký trạm", Amount = registrationRevenue, Percent = totalByType > 0 ? Math.Round(registrationRevenue / totalByType * 100, 1) : 0 },
                new() { Label = "Phí duy trì trạm", Amount = maintenanceRevenue, Percent = totalByType > 0 ? Math.Round(maintenanceRevenue / totalByType * 100, 1) : 0 }
            };

            var model = new RevenueStatisticsViewModel
            {
                TotalRevenue = totalRevenue,
                TodayRevenue = todayRevenue,
                YesterdayRevenue = yesterdayRevenue,
                ThisWeekRevenue = thisWeekRevenue,
                LastWeekRevenue = lastWeekRevenue,
                ThisMonthRevenue = thisMonthRevenue,
                RegistrationRevenue = registrationRevenue,
                MaintenanceRevenue = maintenanceRevenue,
                TodayCompare = BuildCompare(todayRevenue, yesterdayRevenue, "So với hôm qua"),
                WeekCompare = BuildCompare(thisWeekRevenue, lastWeekRevenue, "So với tuần trước"),
                MonthCompare = BuildCompare(thisMonthRevenue, lastMonthRevenue, "So với tháng trước"),
                TotalCompare = BuildCompare(todayRevenue, dayBeforeYesterdayRevenue, "Xu hướng gần nhất"),
                SuccessTransactionCount = successCount,
                PendingTransactionCount = pendingCount,
                FailedTransactionCount = failedCount,
                ActiveStationCount = activeStationCount,
                UnpaidStationCount = unpaidStationCount,
                HiddenStationCount = hiddenStationCount,
                DailyRevenueChartData = dailyRevenue,
                TodayYesterdayChartData = new TodayYesterdayChartViewModel { Today = todayRevenue, Yesterday = yesterdayRevenue, Difference = todayRevenue - yesterdayRevenue, Percent = CalculatePercent(todayRevenue, yesterdayRevenue), CompareText = BuildCompare(todayRevenue, yesterdayRevenue, "So với hôm qua").Text },
                WeekCompareChartData = new WeekCompareChartViewModel { ThisWeekRegistration = thisWeekRegistration, ThisWeekMaintenance = thisWeekMaintenance, LastWeekRegistration = lastWeekRegistration, LastWeekMaintenance = lastWeekMaintenance },
                RevenueByTypeChartData = revenueByType,
                RecentTransactions = recentEntities.Select((x, index) => BuildRecentTransaction(x, (page - 1) * pageSize + index + 1)).ToList(),
                Filter = new RevenueStatisticsFilterViewModel { FromDate = fromDate, ToDate = toDate, Type = type, Status = status, OwnerKeyword = ownerKeyword, StationKeyword = stationKeyword },
                Page = page,
                PageSize = pageSize,
                TotalTransactions = totalTransactions,
                TotalPages = totalPages
            };

            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var transaction = await _context.PaymentTransactions
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Station)
                .Include(x => x.RegistrationRequest)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(DateTime? fromDate, DateTime? toDate, string type = "all", string status = "all", string ownerKeyword = "", string stationKeyword = "")
        {
            type = NormalizeFilter(type);
            status = NormalizeFilter(status);
            ownerKeyword = (ownerKeyword ?? string.Empty).Trim();
            stationKeyword = (stationKeyword ?? string.Empty).Trim();

            var query = ApplyCommonFilters(_context.PaymentTransactions.AsNoTracking(), type, ownerKeyword, stationKeyword, fromDate, toDate, true);
            query = ApplyStatusFilter(query, status).Include(x => x.User).Include(x => x.Station).Include(x => x.RegistrationRequest);

            var items = await query.OrderByDescending(x => x.PaidAt ?? x.CancelledAt ?? x.CreatedAt).Take(5000).ToListAsync();
            var builder = new StringBuilder();
            builder.AppendLine("STT,Mã giao dịch,Chủ trạm,Tên trạm,Loại phí,Số tiền,Phương thức,Trạng thái,Thời gian");

            for (var i = 0; i < items.Count; i++)
            {
                var item = BuildRecentTransaction(items[i], i + 1);
                builder.AppendLine(string.Join(",", new[]
                {
                    item.Index.ToString(),
                    EscapeCsv(item.TransactionCode),
                    EscapeCsv(item.OwnerName),
                    EscapeCsv(item.StationName),
                    EscapeCsv(item.PaymentType),
                    item.Amount.ToString("0"),
                    EscapeCsv(item.PaymentMethod),
                    EscapeCsv(item.Status),
                    EscapeCsv(item.ActivityAt.ToString("dd/MM/yyyy HH:mm"))
                }));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(bytes, "text/csv", $"thong-ke-doanh-thu-{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        private static IQueryable<PaymentTransaction> ApplyCommonFilters(IQueryable<PaymentTransaction> query, string type, string ownerKeyword, string stationKeyword, DateTime? fromDate, DateTime? toDate, bool includeDateFilter)
        {
            if (type == PaymentTransactionType.InitialRegistration || type == "registration_fee")
            {
                query = query.Where(x => RegistrationTypes.Contains(x.PaymentType));
            }
            else if (type == PaymentTransactionType.Maintenance || type == "maintenance_fee")
            {
                query = query.Where(x => MaintenanceTypes.Contains(x.PaymentType));
            }

            if (!string.IsNullOrWhiteSpace(ownerKeyword))
            {
                var keyword = ownerKeyword.Trim().ToLower();
                query = query.Where(x => x.User != null && (((x.User.FullName ?? string.Empty).ToLower().Contains(keyword)) || ((x.User.Username ?? string.Empty).ToLower().Contains(keyword)) || ((x.User.Email ?? string.Empty).ToLower().Contains(keyword))));
            }

            if (!string.IsNullOrWhiteSpace(stationKeyword))
            {
                var keyword = stationKeyword.Trim().ToLower();
                query = query.Where(x => (x.Station != null && (((x.Station.Name ?? string.Empty).ToLower().Contains(keyword)) || ((x.Station.Address ?? string.Empty).ToLower().Contains(keyword)))) || (x.RegistrationRequest != null && ((x.RegistrationRequest.StationName ?? string.Empty).ToLower().Contains(keyword))));
            }

            if (includeDateFilter && fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(x => (x.PaidAt ?? x.CreatedAt) >= start);
            }

            if (includeDateFilter && toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(x => (x.PaidAt ?? x.CreatedAt) < end);
            }

            return query;
        }

        private static IQueryable<PaymentTransaction> ApplyStatusFilter(IQueryable<PaymentTransaction> query, string status)
        {
            status = NormalizeFilter(status);
            return status switch
            {
                "success" => query.Where(x => SuccessStatuses.Contains(x.Status)),
                "pending" => query.Where(x => PendingStatuses.Contains(x.Status)),
                "failed" => query.Where(x => FailedStatuses.Contains(x.Status)),
                "expired" => query.Where(x => x.Status == "expired" || x.Status == "Expired" || x.Status == "Hết hạn"),
                "cancelled" => query.Where(x => x.Status == PaymentTransactionStatus.Cancelled || x.Status == "cancelled" || x.Status == "Cancelled" || x.Status == "Đã hủy thanh toán"),
                "all" => query,
                _ => query.Where(x => x.Status == status)
            };
        }
        private static async Task<decimal> SumAmountAsync(IQueryable<PaymentTransaction> query)
        {
            return await query.SumAsync(x => (decimal?)x.Amount) ?? 0m;
        }

        private static DateTime GetMonday(DateTime date)
        {
            var diff = ((int)date.DayOfWeek + 6) % 7;
            return date.Date.AddDays(-diff);
        }

        private static CompareResultViewModel BuildCompare(decimal current, decimal previous, string prefix)
        {
            var difference = current - previous;
            var percent = CalculatePercent(current, previous);

            if (previous == 0 && current > 0)
            {
                return new CompareResultViewModel { Difference = difference, Percent = null, Text = $"{prefix}: Tăng mới", Trend = "up", IconClass = "fas fa-arrow-up" };
            }

            if (current == 0 && previous == 0)
            {
                return new CompareResultViewModel { Difference = 0, Percent = 0, Text = $"{prefix}: Không thay đổi", Trend = "same", IconClass = "fas fa-minus" };
            }

            var trend = difference > 0 ? "up" : difference < 0 ? "down" : "same";
            var icon = difference > 0 ? "fas fa-arrow-up" : difference < 0 ? "fas fa-arrow-down" : "fas fa-minus";
            var sign = difference > 0 ? "+" : string.Empty;
            var percentText = percent.HasValue ? $"{sign}{percent.Value:N1}%" : "0%";
            return new CompareResultViewModel { Difference = difference, Percent = percent, Text = $"{prefix}: {percentText}", Trend = trend, IconClass = icon };
        }

        private static decimal? CalculatePercent(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? null : 0;
            return Math.Round((current - previous) / previous * 100, 1);
        }

        private static RecentTransactionViewModel BuildRecentTransaction(PaymentTransaction item, int index)
        {
            var ownerName = item.User?.FullName ?? item.User?.Username ?? item.User?.Email ?? "-";
            var stationName = item.Station?.Name ?? item.RegistrationRequest?.StationName ?? "-";
            return new RecentTransactionViewModel
            {
                Id = item.Id,
                Index = index,
                TransactionCode = item.PayOsOrderCode.HasValue ? item.PayOsOrderCode.Value.ToString() : $"GD{item.Id:D6}",
                OwnerName = ownerName,
                StationName = stationName,
                PaymentType = PaymentTransactionType.ToDisplay(item.PaymentType),
                Amount = item.Amount,
                PaymentMethod = item.PayOsOrderCode.HasValue ? "PayOS" : "Hệ thống",
                Status = DisplayStatus(item.Status),
                StatusKey = GetStatusKey(item.Status),
                ActivityAt = item.PaidAt ?? item.CancelledAt ?? item.CreatedAt
            };
        }

        private static string DisplayStatus(string? status)
        {
            if (SuccessStatuses.Contains(status ?? string.Empty)) return "Thành công";
            if (PendingStatuses.Contains(status ?? string.Empty)) return "Đang chờ";
            if ((status ?? string.Empty).Equals("expired", StringComparison.OrdinalIgnoreCase) || status == "Hết hạn") return "Hết hạn";
            if (FailedStatuses.Contains(status ?? string.Empty)) return "Thất bại";
            return string.IsNullOrWhiteSpace(status) ? "Không rõ" : status!;
        }

        private static string GetStatusKey(string? status)
        {
            if (SuccessStatuses.Contains(status ?? string.Empty)) return "success";
            if (PendingStatuses.Contains(status ?? string.Empty)) return "pending";
            if ((status ?? string.Empty).Equals("expired", StringComparison.OrdinalIgnoreCase) || status == "Hết hạn") return "expired";
            if (FailedStatuses.Contains(status ?? string.Empty)) return "failed";
            return "other";
        }

        private static string NormalizeFilter(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "all" : value.Trim();
        }

        private static string EscapeCsv(string? value)
        {
            value ??= string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                value = $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }
}
