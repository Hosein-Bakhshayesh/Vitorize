using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Coupons;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Coupons
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class CreateModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public CreateModel(ApiClient apiClient) => _apiClient = apiClient;
        [BindProperty] public AdminCouponInputModel Input { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public IActionResult OnGet()
        {
            Input.IsActive = true; Input.DiscountType = 1;
            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            var result = await _apiClient.PostAsync<AdminCouponModel>("admin/coupons", Input);
            if (!result.IsSuccess || result.Data == null) { ErrorMessage = result.Message; return Page(); }
            TempData["SuccessMessage"] = "کوپن با موفقیت ذخیره شد.";
            return RedirectToPage("Index");
        }
    }
}
