using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Common;
using Vitorize.Web.Models.Admin.Reviews;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Reviews
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;

        public List<AdminProductReviewModel> Reviews { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }

        [BindProperty(SupportsGet = true)] public string? Status { get; set; }
        [BindProperty(SupportsGet = true)] public byte? Rating { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;
        public int PageSize { get; } = 20;

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            if (PageNumber < 1)
                PageNumber = 1;

            var result = await _apiClient.GetAsync<AdminPagedResultModel<AdminProductReviewModel>>(
                "admin/product-reviews" + BuildQuery(Status, PageNumber, PageSize, applyFilters: true));

            if (result.IsSuccess && result.Data != null)
            {
                Reviews = result.Data.Rows;
                TotalCount = result.Data.TotalCount;
                TotalPages = result.Data.TotalPages;
            }
            else
            {
                ErrorMessage = result.Message;
            }

            PendingCount = await CountAsync("pending");
            ApprovedCount = await CountAsync("approved");
            RejectedCount = await CountAsync("rejected");
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid id)
        {
            var result = await _apiClient.PostAsync<AdminProductReviewModel>(
                $"admin/product-reviews/{id}/approve", new { });

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "نظر با موفقیت تأیید و منتشر شد." : result.Message;

            return RedirectToPage(new { Status, Rating, Search, page = PageNumber });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync($"admin/product-reviews/{id}");

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "نظر با موفقیت حذف شد." : result.Message;

            return RedirectToPage(new { Status, Rating, Search, page = PageNumber });
        }

        private async Task<int> CountAsync(string status)
        {
            var result = await _apiClient.GetAsync<AdminPagedResultModel<AdminProductReviewModel>>(
                "admin/product-reviews" + BuildQuery(status, 1, 1, applyFilters: false));

            return result.IsSuccess && result.Data != null ? result.Data.TotalCount : 0;
        }

        private string BuildQuery(string? status, int page, int pageSize, bool applyFilters)
        {
            var query = new List<string>
            {
                "page=" + page,
                "pageSize=" + pageSize
            };

            switch (status)
            {
                case "pending":
                    query.Add("isApproved=false");
                    query.Add("isRejected=false");
                    break;
                case "approved":
                    query.Add("isApproved=true");
                    break;
                case "rejected":
                    query.Add("isRejected=true");
                    break;
            }

            if (applyFilters)
            {
                if (Rating is >= 1 and <= 5)
                    query.Add("rating=" + Rating.Value);

                if (!string.IsNullOrWhiteSpace(Search))
                    query.Add("search=" + Uri.EscapeDataString(Search.Trim()));
            }

            return "?" + string.Join("&", query);
        }
    }
}
