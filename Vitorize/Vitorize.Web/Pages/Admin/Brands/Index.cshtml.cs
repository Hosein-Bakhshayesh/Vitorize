using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Models.Admin.Brands;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Brands
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme)]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;

        public IndexModel(
            ApiClient apiClient,
            IFileStorageService fileStorage)
        {
            _apiClient = apiClient;
            _fileStorage = fileStorage;
        }

        public List<AdminBrandModel> Brands { get; set; } = new();

        [BindProperty]
        public AdminBrandInputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.Title))
            {
                ErrorMessage = "عنوان برند الزامی است.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(Input.Slug))
            {
                Input.Slug = GenerateSlug(Input.Title);
            }

            AdminBrandModel? oldBrand = null;

            if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
            {
                var oldResult = await _apiClient.GetAsync<AdminBrandModel>(
                    $"admin/brands/{Input.Id.Value}");

                if (oldResult.IsSuccess)
                    oldBrand = oldResult.Data;
            }

            var uploadedImagePath = await _fileStorage.SaveAsync(
                ImageFile,
                StorageFolders.Brands);

            if (!string.IsNullOrWhiteSpace(uploadedImagePath))
            {
                if (!string.IsNullOrWhiteSpace(oldBrand?.ImagePath))
                    await _fileStorage.DeleteAsync(oldBrand.ImagePath);

                Input.ImagePath = uploadedImagePath;
            }
            else if (oldBrand != null)
            {
                Input.ImagePath = oldBrand.ImagePath;
            }

            var result = Input.Id.HasValue && Input.Id.Value != Guid.Empty
                ? await _apiClient.PutAsync<AdminBrandModel>($"admin/brands/{Input.Id.Value}", Input)
                : await _apiClient.PostAsync<AdminBrandModel>("admin/brands", Input);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message;
                return RedirectToPage();
            }

            SuccessMessage = Input.Id.HasValue && Input.Id.Value != Guid.Empty
                ? "برند با موفقیت ویرایش شد."
                : "برند با موفقیت ایجاد شد.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var oldResult = await _apiClient.GetAsync<AdminBrandModel>($"admin/brands/{id}");

            var result = await _apiClient.DeleteAsync($"admin/brands/{id}");

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message;
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(oldResult.Data?.ImagePath))
                await _fileStorage.DeleteAsync(oldResult.Data.ImagePath);

            SuccessMessage = "برند با موفقیت حذف شد.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminBrandModel>>("admin/brands");

            Brands = result.IsSuccess && result.Data != null
                ? result.Data
                : new List<AdminBrandModel>();
        }

        private static string GenerateSlug(string title)
        {
            return title
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("\\", "-");
        }
    }
}