using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Banners;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Banners
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class CreateModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;

        public CreateModel(ApiClient apiClient, IFileStorageService fileStorage)
        {
            _apiClient = apiClient;
            _fileStorage = fileStorage;
        }

        [BindProperty] public AdminBannerInputModel Input { get; set; } = new();
        [BindProperty] public IFormFile? ImageFile { get; set; }
        [BindProperty] public IFormFile? MobileImageFile { get; set; }
        public string? ErrorMessage { get; set; }
        public List<AdminBannerPositionOption> Positions => BannerPositions.All;

        public IActionResult OnGet()
        {
            Input.IsActive = true;
            Input.Position = "home-hero";
            Input.SortOrder = 10;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ImageFile == null || ImageFile.Length == 0)
                ModelState.AddModelError(nameof(ImageFile), "تصویر اصلی بنر الزامی است.");

            if (!ModelState.IsValid)
                return Page();

            string? newImagePath = null;
            string? newMobileImagePath = null;

            try
            {
                newImagePath = await _fileStorage.SaveAsync(ImageFile, StorageFolders.Banners);
                newMobileImagePath = await _fileStorage.SaveAsync(MobileImageFile, StorageFolders.Banners);

                Input.ImagePath = newImagePath ?? string.Empty;
                Input.MobileImagePath = newMobileImagePath;

                var result = await _apiClient.PostAsync<AdminBannerModel>("admin/banners", Input);

                if (!result.IsSuccess)
                {
                    await _fileStorage.DeleteAsync(newImagePath);
                    await _fileStorage.DeleteAsync(newMobileImagePath);
                    ErrorMessage = result.Message;
                    return Page();
                }

                TempData["SuccessMessage"] = "بنر با موفقیت ایجاد شد.";
                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                await _fileStorage.DeleteAsync(newImagePath);
                await _fileStorage.DeleteAsync(newMobileImagePath);
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
    }
}
