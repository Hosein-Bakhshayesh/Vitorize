using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Orders;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Orders
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminOrderModel Order { get; set; } = new();
        [BindProperty] public string? Reason { get; set; }
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminOrderModel>("admin/orders/" + id);
            if (!result.IsSuccess || result.Data == null) { TempData["ErrorMessage"] = result.Message; return RedirectToPage("Index"); }
            Order = result.Data; return Page();
        }
        public async Task<IActionResult> OnPostCompleteAsync(Guid id)
        {
            var result = await _apiClient.PostAsync<object>($"admin/orders/{id}/complete", new { });
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "سفارش تکمیل شد." : result.Message;
            return RedirectToPage(new { id });
        }
        public async Task<IActionResult> OnPostCancelAsync(Guid id)
        {
            var result = await _apiClient.PostAsync<object>($"admin/orders/{id}/cancel", new CancelOrderRequestModel { Reason = Reason ?? "لغو توسط مدیر" });
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "سفارش لغو شد." : result.Message;
            return RedirectToPage(new { id });
        }
    }
}
