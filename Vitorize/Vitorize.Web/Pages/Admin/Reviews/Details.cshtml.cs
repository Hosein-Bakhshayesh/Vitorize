using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Reviews;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Reviews
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;

        public AdminProductReviewModel Item { get; set; } = new();
        [BindProperty] public RejectReviewRequestModel Reject { get; set; } = new();
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminProductReviewModel>("admin/product-reviews/" + id);

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }

            Item = result.Data;
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid id)
        {
            var result = await _apiClient.PostAsync<AdminProductReviewModel>(
                $"admin/product-reviews/{id}/approve", new { });

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "نظر با موفقیت تأیید و منتشر شد." : result.Message;

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid id)
        {
            if (!ModelState.IsValid)
            {
                await LoadItemAsync(id);
                return Page();
            }

            var result = await _apiClient.PostAsync<AdminProductReviewModel>(
                $"admin/product-reviews/{id}/reject", new { reason = Reject.Reason });

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "نظر رد شد و برای کاربر قابل مشاهده نخواهد بود." : result.Message;

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync($"admin/product-reviews/{id}");

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "نظر با موفقیت حذف شد.";
                return RedirectToPage("Index");
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToPage(new { id });
        }

        private async Task LoadItemAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminProductReviewModel>("admin/product-reviews/" + id);

            if (result.IsSuccess && result.Data != null)
                Item = result.Data;
        }
    }
}
