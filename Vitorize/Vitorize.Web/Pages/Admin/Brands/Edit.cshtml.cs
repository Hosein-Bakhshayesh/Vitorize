using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Brands;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Brands
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;
        public EditModel(ApiClient apiClient, IFileStorageService fileStorage)
        { _apiClient = apiClient; _fileStorage = fileStorage; }
        [BindProperty] public AdminBrandInputModel Input { get; set; } = new();
        [BindProperty] public IFormFile? ImageFile { get; set; }
        [BindProperty] public bool RemoveImage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {

            var result = await _apiClient.GetAsync<AdminBrandModel>("admin/brands/" + id);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }
            Input = new AdminBrandInputModel
            {
                Id = result.Data.Id,
                Title = result.Data.Title,
                Slug = result.Data.Slug,
                ImagePath = result.Data.ImagePath,
                IsActive = result.Data.IsActive
            };
            return Page();

        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.Slug)) Input.Slug = GenerateSlug(Input.Title);
            if (!ModelState.IsValid) return Page();

            string? oldImagePath = null;
            if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
            {
                var old = await _apiClient.GetAsync<AdminBrandModel>("admin/brands/" + Input.Id.Value);
                oldImagePath = old.Data?.ImagePath;
                if (string.IsNullOrWhiteSpace(Input.ImagePath)) Input.ImagePath = oldImagePath;
            }

            string? newImagePath = null;
            try
            {
                if (RemoveImage) Input.ImagePath = null;
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    newImagePath = await _fileStorage.SaveAsync(ImageFile, StorageFolders.Brands);
                    Input.ImagePath = newImagePath;
                }

                var result = await _apiClient.PutAsync<AdminBrandModel>("admin/brands/" + Input.Id!.Value, Input);
                if (!result.IsSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(newImagePath)) await _fileStorage.DeleteAsync(newImagePath);
                    ErrorMessage = result.Message;
                    return Page();
                }

                if ((RemoveImage || !string.IsNullOrWhiteSpace(newImagePath)) && !string.IsNullOrWhiteSpace(oldImagePath))
                    await _fileStorage.DeleteAsync(oldImagePath);

                TempData["SuccessMessage"] = "برند با موفقیت ذخیره شد.";
                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newImagePath)) await _fileStorage.DeleteAsync(newImagePath);
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
        private static string GenerateSlug(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
    }
}
