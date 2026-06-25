using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Brands;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Brands
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;

        public IndexModel(ApiClient apiClient, IFileStorageService fileStorage)
        {
            _apiClient = apiClient;
            _fileStorage = fileStorage;
        }

        public List<AdminBrandModel> Items { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public int TotalCount => Items.Count;
        public int ActiveCount => Items.Count(x => x.IsActive);
        public int InactiveCount => Items.Count(x => !x.IsActive);
        public int WithImageCount => Items.Count(x => !string.IsNullOrWhiteSpace(x.ImagePath));

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var oldResult = await _apiClient.GetAsync<AdminBrandModel>("admin/brands/" + id);
            var result = await _apiClient.DeleteAsync("admin/brands/" + id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(oldResult.Data?.ImagePath))
                await _fileStorage.DeleteAsync(oldResult.Data.ImagePath);

            TempData["SuccessMessage"] = "برند با موفقیت حذف شد.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminBrandModel>>("admin/brands");
            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                Items = new();
                return;
            }

            Items = result.Data;
            if (!string.IsNullOrWhiteSpace(Search))
            {
                Items = Items.Where(x =>
                    x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    x.Slug.Contains(Search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            Items = Items.OrderByDescending(x => x.IsActive).ThenBy(x => x.Title).ToList();
        }

        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
    }
}
