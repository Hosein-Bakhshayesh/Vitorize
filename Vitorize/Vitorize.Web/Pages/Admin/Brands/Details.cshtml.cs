using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Brands;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Brands
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminBrandModel Item { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminBrandModel>("admin/brands/" + id);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }
            Item = result.Data;
            return Page();
        }
        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
    }
}
