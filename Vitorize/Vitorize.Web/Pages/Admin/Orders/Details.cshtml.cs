using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Orders;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Orders
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public DetailsModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public OrderModel Order { get; set; } = new();

        [BindProperty]
        public string? CancelReason { get; set; }

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            await LoadAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnPostCompleteAsync(Guid id)
        {
            var result = await _apiClient.PostAsync<object>(
                $"admin/orders/{id}/complete",
                new { });

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "سفارش با موفقیت تکمیل شد."
                    : result.Message;

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCancelAsync(Guid id)
        {
            var request = new CancelOrderRequestModel
            {
                Reason = CancelReason
            };

            var result = await _apiClient.PostAsync<object>(
                $"admin/orders/{id}/cancel",
                request);

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "سفارش با موفقیت لغو شد."
                    : result.Message;

            return RedirectToPage(new { id });
        }

        private async Task LoadAsync(Guid id)
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            ErrorMessage = TempData["ErrorMessage"]?.ToString();

            var result = await _apiClient.GetAsync<OrderModel>(
                $"admin/orders/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return;
            }

            Order = result.Data;
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string FormatDate(DateTime? date)
        {
            return date.HasValue
                ? date.Value.ToString("yyyy/MM/dd HH:mm")
                : "-";
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

        public string GetDeliveryType(byte type)
        {
            return type switch
            {
                1 => "تحویل آنی",
                2 => "تحویل دستی",
                3 => "تیکتی",
                _ => "نامشخص"
            };
        }

        public string GetDeliveryStatus(byte status)
        {
            return status switch
            {
                1 => "در انتظار",
                2 => "تحویل شده",
                3 => "بررسی دستی",
                4 => "ناموفق",
                _ => "نامشخص"
            };
        }
    }
}