using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Coupons;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Coupons
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<AdminCouponModel> Coupons { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task OnGetAsync() => await LoadAsync();
        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync("admin/coupons/" + id);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "کوپن حذف شد." : result.Message;
            return RedirectToPage();
        }
        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminCouponModel>>("admin/coupons");
            Coupons = result.IsSuccess && result.Data != null ? result.Data : new();
            if (!string.IsNullOrWhiteSpace(Search))
                Coupons = Coupons.Where(x => x.Code.Contains(Search, StringComparison.OrdinalIgnoreCase) || x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();
            Coupons = Coupons.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreatedAt).ToList();
        }
    }
}
