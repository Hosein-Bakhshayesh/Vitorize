using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Dashboard;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public AdminDashboardModel Dashboard { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public bool HasDataLoaded { get; set; }

        public bool IsDashboardEmpty
        {
            get
            {
                var summary = Dashboard.Summary;

                return summary.TotalUsers == 0 &&
                       summary.NewUsersToday == 0 &&
                       summary.TotalOrders == 0 &&
                       summary.OrdersToday == 0 &&
                       summary.RevenueToday == 0 &&
                       summary.RevenueThisMonth == 0 &&
                       summary.TotalWalletBalance == 0 &&
                       summary.PendingTickets == 0 &&
                       summary.PendingVerifications == 0 &&
                       summary.UnreadNotifications == 0 &&
                       summary.AvailableGiftCodes == 0 &&
                       summary.ReservedGiftCodes == 0 &&
                       summary.SoldGiftCodes == 0 &&
                       !Dashboard.TopProducts.Any() &&
                       !Dashboard.SalesLast7Days.Any(x => x.Value > 0) &&
                       !Dashboard.OrdersLast7Days.Any(x => x.Value > 0);
            }
        }

        public bool HasChartData =>
            Dashboard.SalesLast7Days.Any(x => x.Value > 0) ||
            Dashboard.OrdersLast7Days.Any(x => x.Value > 0);

        public async Task OnGetAsync()
        {
            var result = await _apiClient.GetAsync<AdminDashboardModel>(
                "admin/dashboard");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "امکان دریافت اطلاعات داشبورد وجود ندارد."
                    : result.Message;

                HasDataLoaded = false;
                return;
            }

            Dashboard = result.Data;
            HasDataLoaded = true;
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        public string FormatNumber(decimal value)
        {
            return value.ToString("N0");
        }

        public string FormatChartMoney(decimal value)
        {
            if (value <= 0)
                return string.Empty;

            if (value >= 1_000_000_000)
                return (value / 1_000_000_000).ToString("N1") + "B";

            if (value >= 1_000_000)
                return (value / 1_000_000).ToString("N1") + "M";

            if (value >= 1_000)
                return (value / 1_000).ToString("N0") + "K";

            return value.ToString("N0");
        }

        public string FormatChartCount(decimal value)
        {
            if (value <= 0)
                return string.Empty;

            return value.ToString("N0");
        }

        public string FormatShortDate(DateTime date)
        {
            return date.DayOfWeek switch
            {
                DayOfWeek.Saturday => "شنبه",
                DayOfWeek.Sunday => "یکشنبه",
                DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه‌شنبه",
                DayOfWeek.Wednesday => "چهارشنبه",
                DayOfWeek.Thursday => "پنجشنبه",
                DayOfWeek.Friday => "جمعه",
                _ => date.ToString("MM/dd")
            };
        }

        public int GetCompletedOrderPercent()
        {
            var completed = GetOrderStatusCount(3);

            if (Dashboard.Summary.TotalOrders <= 0)
                return 0;

            var percent = (decimal)completed / Dashboard.Summary.TotalOrders * 100;

            return ClampPercent((int)Math.Round(percent));
        }

        public int GetPendingOrderPercent()
        {
            var pending = GetOrderStatusCount(1) + GetOrderStatusCount(2);

            if (Dashboard.Summary.TotalOrders <= 0)
                return 0;

            var percent = (decimal)pending / Dashboard.Summary.TotalOrders * 100;

            return ClampPercent((int)Math.Round(percent));
        }

        public int GetGiftCodeStockPercent()
        {
            var total =
                Dashboard.Summary.AvailableGiftCodes +
                Dashboard.Summary.ReservedGiftCodes +
                Dashboard.Summary.SoldGiftCodes;

            if (total <= 0)
                return 0;

            var percent = (decimal)Dashboard.Summary.AvailableGiftCodes / total * 100;

            return ClampPercent((int)Math.Round(percent));
        }

        public int GetOrderStatusCount(byte status)
        {
            return Dashboard.OrderStatusCounts
                .FirstOrDefault(x => x.Status == status)
                ?.Count ?? 0;
        }

        public int GetPaymentStatusCount(byte status)
        {
            return Dashboard.PaymentStatusCounts
                .FirstOrDefault(x => x.Status == status)
                ?.Count ?? 0;
        }

        public string GetOrderStatusTitle(byte status)
        {
            return status switch
            {
                1 => "در انتظار",
                2 => "در حال پردازش",
                3 => "تکمیل شده",
                4 => "لغو شده",
                _ => "نامشخص"
            };
        }

        public string GetPaymentStatusTitle(byte status)
        {
            return status switch
            {
                1 => "در انتظار",
                2 => "پرداخت شده",
                3 => "ناموفق",
                4 => "برگشت خورده",
                _ => "نامشخص"
            };
        }

        public string GetStatusCss(byte status)
        {
            return status switch
            {
                1 => "vd-status vd-status-warning",
                2 => "vd-status vd-status-info",
                3 => "vd-status vd-status-success",
                4 => "vd-status vd-status-danger",
                _ => "vd-status vd-status-muted"
            };
        }

        public int GetChartHeight(decimal value, decimal max)
        {
            if (value <= 0 || max <= 0)
                return 0;

            var percent = (value / max) * 100;
            var result = (int)Math.Round(percent);

            if (result < 8)
                result = 8;

            return ClampPercent(result);
        }

        public decimal GetMaxSalesValue()
        {
            return Dashboard.SalesLast7Days
                .Select(x => x.Value)
                .DefaultIfEmpty(0)
                .Max();
        }

        public decimal GetMaxOrdersValue()
        {
            return Dashboard.OrdersLast7Days
                .Select(x => x.Value)
                .DefaultIfEmpty(0)
                .Max();
        }

        public List<DashboardChartPointModel> GetSalesPoints()
        {
            return Dashboard.SalesLast7Days
                .OrderBy(x => x.Date)
                .Take(7)
                .ToList();
        }

        public List<DashboardChartPointModel> GetOrderPoints()
        {
            return Dashboard.OrdersLast7Days
                .OrderBy(x => x.Date)
                .Take(7)
                .ToList();
        }

        private static int ClampPercent(int value)
        {
            if (value < 0)
                return 0;

            if (value > 100)
                return 100;

            return value;
        }
    }
}