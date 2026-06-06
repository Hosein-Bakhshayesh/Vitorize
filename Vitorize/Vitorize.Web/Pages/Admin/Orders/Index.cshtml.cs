using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Orders;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Orders
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public List<OrderModel> Orders { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? OrderNumber { get; set; }

        [BindProperty(SupportsGet = true)]
        public byte? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public byte? PaymentStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public int TotalOrders => Orders.Count;

        public int PendingOrders => Orders.Count(x => x.Status == 1);

        public int CompletedOrders => Orders.Count(x => x.Status == 3);

        public int CancelledOrders => Orders.Count(x => x.Status == 4);

        public decimal TotalRevenue => Orders
            .Where(x => x.PaymentStatus == 2)
            .Sum(x => x.FinalAmount);

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            ErrorMessage = TempData["ErrorMessage"]?.ToString();

            var hasFilter =
                !string.IsNullOrWhiteSpace(OrderNumber) ||
                Status.HasValue ||
                PaymentStatus.HasValue ||
                FromDate.HasValue ||
                ToDate.HasValue;

            if (!hasFilter)
            {
                var allResult = await _apiClient.GetAsync<List<OrderModel>>("admin/orders");

                if (!allResult.IsSuccess || allResult.Data == null)
                {
                    ErrorMessage = allResult.Message;
                    return;
                }

                Orders = allResult.Data
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList();

                return;
            }

            var query = BuildQueryString();

            var result = await _apiClient.GetAsync<List<OrderModel>>(
                $"admin/orders/search{query}");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return;
            }

            Orders = result.Data
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }

        private string BuildQueryString()
        {
            var parameters = new List<string>();

            if (!string.IsNullOrWhiteSpace(OrderNumber))
                parameters.Add($"OrderNumber={Uri.EscapeDataString(OrderNumber)}");

            if (Status.HasValue)
                parameters.Add($"Status={Status.Value}");

            if (PaymentStatus.HasValue)
                parameters.Add($"PaymentStatus={PaymentStatus.Value}");

            if (FromDate.HasValue)
                parameters.Add($"FromDate={FromDate.Value:yyyy-MM-dd}");

            if (ToDate.HasValue)
                parameters.Add($"ToDate={ToDate.Value:yyyy-MM-dd}");

            return parameters.Count == 0
                ? string.Empty
                : "?" + string.Join("&", parameters);
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string FormatDate(DateTime date)
        {
            return date.ToString("yyyy/MM/dd HH:mm");
        }

        public string GetOrderStatus(byte status)
        {
            return status switch
            {
                1 => "در انتظار پرداخت",
                2 => "در حال پردازش",
                3 => "تکمیل شده",
                4 => "لغو شده",
                5 => "ناموفق",
                6 => "مرجوع شده",
                _ => "نامشخص"
            };
        }

        public string GetOrderStatusClass(byte status)
        {
            return status switch
            {
                1 => "bg-amber-50 text-amber-700",
                2 => "bg-cyan-50 text-cyan-700",
                3 => "bg-emerald-50 text-emerald-700",
                4 => "bg-rose-50 text-rose-700",
                5 => "bg-red-50 text-red-700",
                6 => "bg-purple-50 text-purple-700",
                _ => "bg-slate-100 text-slate-600"
            };
        }

        public string GetPaymentStatus(byte status)
        {
            return status switch
            {
                1 => "در انتظار",
                2 => "پرداخت شده",
                3 => "ناموفق",
                4 => "لغو شده",
                5 => "مرجوع شده",
                _ => "نامشخص"
            };
        }

        public string GetPaymentStatusClass(byte status)
        {
            return status switch
            {
                1 => "bg-amber-50 text-amber-700",
                2 => "bg-emerald-50 text-emerald-700",
                3 => "bg-rose-50 text-rose-700",
                4 => "bg-slate-100 text-slate-600",
                5 => "bg-purple-50 text-purple-700",
                _ => "bg-slate-100 text-slate-600"
            };
        }
    }
}