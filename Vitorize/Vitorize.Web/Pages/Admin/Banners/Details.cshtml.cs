using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Banners;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Banners
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public DetailsModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public AdminBannerModel Banner { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminBannerModel>($"admin/banners/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }

            Banner = result.Data;
            return Page();
        }

        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
        public string Date(DateTime? value) => AdminUiHelper.Date(value);
        public string PositionTitle(string? value) => BannerPositions.Title(value);
    }
}
