using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Coupons;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Coupons
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public EditModel(ApiClient apiClient) => _apiClient = apiClient;
        [BindProperty] public AdminCouponInputModel Input {
            get;
            set;
        }
        = new();
        public string? ErrorMessage {
            get;
            set;
        }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminCouponModel>("admin/coupons/" + id);
            if (!result.IsSuccess || result.Data == null) {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }
            Input = new AdminCouponInputModel {
                Code = result.Data.Code, Title = result.Data.Title, DiscountType = result.Data.DiscountType, DiscountValue = result.Data.DiscountValue, MaxUsageCount = result.Data.MaxUsageCount, MaxUsagePerUser = result.Data.MaxUsagePerUser, MinOrderAmount = result.Data.MinOrderAmount, StartsAt = result.Data.StartsAt, EndsAt = result.Data.EndsAt, IsActive = result.Data.IsActive }
                ;
                return Page();
            }
            public async Task<IActionResult> OnPostAsync(Guid id)
            {
                if (!ModelState.IsValid) return Page();
                var result = await _apiClient.PutAsync<AdminCouponModel>("admin/coupons/" + id, Input);
                if (!result.IsSuccess || result.Data == null) {
                    ErrorMessage = result.Message;
                    return Page();
                }
                TempData["SuccessMessage"] = "کوپن با موفقیت ذخیره شد.";
                return RedirectToPage("Index");
            }
        }
    }
