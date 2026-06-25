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
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;

        public EditModel(ApiClient apiClient, IFileStorageService fileStorage)
        {
            _apiClient = apiClient;
            _fileStorage = fileStorage;
        }

        [BindProperty] public AdminBannerInputModel Input { get; set; } = new();
        [BindProperty] public IFormFile? ImageFile { get; set; }
        [BindProperty] public IFormFile? MobileImageFile { get; set; }
        [BindProperty] public bool RemoveMobileImage { get; set; }
        public string? ErrorMessage { get; set; }
        public List<AdminBannerPositionOption> Positions => BannerPositions.All;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminBannerModel>($"admin/banners/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }

            Input = ToInput(result.Data);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var oldResult = await _apiClient.GetAsync<AdminBannerModel>($"admin/banners/{id}");

            if (!oldResult.IsSuccess || oldResult.Data == null)
            {
                TempData["ErrorMessage"] = oldResult.Message;
                return RedirectToPage("Index");
            }

            var old = oldResult.Data;

            if (string.IsNullOrWhiteSpace(Input.ImagePath))
                Input.ImagePath = old.ImagePath;

            if (string.IsNullOrWhiteSpace(Input.MobileImagePath))
                Input.MobileImagePath = old.MobileImagePath;

            if (!ModelState.IsValid)
                return Page();

            string? newImagePath = null;
            string? newMobileImagePath = null;

            try
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    newImagePath = await _fileStorage.SaveAsync(ImageFile, StorageFolders.Banners);
                    Input.ImagePath = newImagePath ?? old.ImagePath;
                }

                if (RemoveMobileImage)
                {
                    Input.MobileImagePath = null;
                }

                if (MobileImageFile != null && MobileImageFile.Length > 0)
                {
                    newMobileImagePath = await _fileStorage.SaveAsync(MobileImageFile, StorageFolders.Banners);
                    Input.MobileImagePath = newMobileImagePath;
                }

                var result = await _apiClient.PutAsync<AdminBannerModel>($"admin/banners/{id}", Input);

                if (!result.IsSuccess)
                {
                    await _fileStorage.DeleteAsync(newImagePath);
                    await _fileStorage.DeleteAsync(newMobileImagePath);
                    ErrorMessage = result.Message;
                    return Page();
                }

                if (!string.IsNullOrWhiteSpace(newImagePath) && !string.IsNullOrWhiteSpace(old.ImagePath))
                    await _fileStorage.DeleteAsync(old.ImagePath);

                if ((RemoveMobileImage || !string.IsNullOrWhiteSpace(newMobileImagePath)) && !string.IsNullOrWhiteSpace(old.MobileImagePath))
                    await _fileStorage.DeleteAsync(old.MobileImagePath);

                TempData["SuccessMessage"] = "بنر با موفقیت ویرایش شد.";
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

        private static AdminBannerInputModel ToInput(AdminBannerModel banner)
        {
            return new AdminBannerInputModel
            {
                Id = banner.Id,
                Title = banner.Title,
                ImagePath = banner.ImagePath,
                MobileImagePath = banner.MobileImagePath,
                LinkUrl = banner.LinkUrl,
                Position = banner.Position,
                SortOrder = banner.SortOrder,
                IsActive = banner.IsActive,
                StartsAt = banner.StartsAt,
                EndsAt = banner.EndsAt
            };
        }

        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
    }
}
