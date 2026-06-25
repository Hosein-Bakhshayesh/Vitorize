using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Coupons;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Coupons
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminCouponModel Coupon { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminCouponModel>("admin/coupons/" + id);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }
            Coupon = result.Data;
            return Page();
        }
    }
}
