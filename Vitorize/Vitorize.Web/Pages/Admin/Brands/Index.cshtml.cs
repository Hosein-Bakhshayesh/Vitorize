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
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;

        private const long MaxImageSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly string[] AllowedImageContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

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

        [BindProperty]
        public bool RemoveImage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool OpenModalOnLoad { get; set; }

        public int TotalBrands => Brands.Count;

        public int ActiveBrandCount => Brands.Count(x => x.IsActive);

        public int InactiveBrandCount => Brands.Count(x => !x.IsActive);

        public int BrandsWithImageCount => Brands.Count(x => !string.IsNullOrWhiteSpace(x.ImagePath));

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            await LoadAsync();

            NormalizeInput();

            ModelState.Clear();
            TryValidateModel(Input, nameof(Input));

            ValidateBusinessRules();

            var imageValidationError = ValidateImageFile(ImageFile);

            if (!string.IsNullOrWhiteSpace(imageValidationError))
            {
                ModelState.AddModelError(nameof(ImageFile), imageValidationError);
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "لطفاً خطاهای فرم را بررسی کنید.";
                OpenModalOnLoad = true;
                return Page();
            }

            AdminBrandModel? oldBrand = null;

            if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
            {
                var oldResult = await _apiClient.GetAsync<AdminBrandModel>(
                    $"admin/brands/{Input.Id.Value}");

                if (oldResult.IsSuccess)
                    oldBrand = oldResult.Data;
            }

            var newImagePath = string.Empty;
            var oldImagePath = oldBrand?.ImagePath;

            try
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    newImagePath = await _fileStorage.SaveAsync(
                        ImageFile,
                        StorageFolders.Brands) ?? string.Empty;

                    Input.ImagePath = newImagePath;
                }
                else if (RemoveImage)
                {
                    Input.ImagePath = null;
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
                    if (!string.IsNullOrWhiteSpace(newImagePath))
                        await _fileStorage.DeleteAsync(newImagePath);

                    ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                        ? "ذخیره برند با خطا مواجه شد."
                        : result.Message;

                    OpenModalOnLoad = true;
                    await LoadAsync();
                    return Page();
                }

                if (!string.IsNullOrWhiteSpace(newImagePath) &&
                    !string.IsNullOrWhiteSpace(oldImagePath) &&
                    !oldImagePath.Equals(newImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    await _fileStorage.DeleteAsync(oldImagePath);
                }

                if (RemoveImage &&
                    string.IsNullOrWhiteSpace(newImagePath) &&
                    !string.IsNullOrWhiteSpace(oldImagePath))
                {
                    await _fileStorage.DeleteAsync(oldImagePath);
                }

                SuccessMessage = Input.Id.HasValue && Input.Id.Value != Guid.Empty
                    ? "برند با موفقیت ویرایش شد."
                    : "برند با موفقیت ایجاد شد.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newImagePath))
                    await _fileStorage.DeleteAsync(newImagePath);

                ErrorMessage = ex.Message;
                OpenModalOnLoad = true;

                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                ErrorMessage = "شناسه برند معتبر نیست.";
                return RedirectToPage();
            }

            var oldResult = await _apiClient.GetAsync<AdminBrandModel>(
                $"admin/brands/{id}");

            var result = await _apiClient.DeleteAsync(
                $"admin/brands/{id}");

            if (!result.IsSuccess)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "حذف برند با خطا مواجه شد."
                    : result.Message;

                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(oldResult.Data?.ImagePath))
                await _fileStorage.DeleteAsync(oldResult.Data.ImagePath);

            SuccessMessage = "برند با موفقیت حذف شد.";

            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminBrandModel>>(
                "admin/brands");

            Brands = result.IsSuccess && result.Data != null
                ? result.Data
                    .OrderBy(x => x.Title)
                    .ToList()
                : new List<AdminBrandModel>();
        }

        private void NormalizeInput()
        {
            Input.Title = Input.Title?.Trim() ?? string.Empty;

            Input.Slug = string.IsNullOrWhiteSpace(Input.Slug)
                ? GenerateSlug(Input.Title)
                : GenerateSlug(Input.Slug);

            Input.ImagePath = NormalizeNullable(Input.ImagePath);

            if (Input.Id == Guid.Empty)
                Input.Id = null;
        }

        private void ValidateBusinessRules()
        {
            if (string.IsNullOrWhiteSpace(Input.Title))
            {
                ModelState.AddModelError(
                    "Input.Title",
                    "عنوان برند الزامی است.");
            }

            if (string.IsNullOrWhiteSpace(Input.Slug))
            {
                ModelState.AddModelError(
                    "Input.Slug",
                    "اسلاگ برند الزامی است.");
            }

            var duplicateSlug = Brands.Any(x =>
                x.Slug.Equals(Input.Slug, StringComparison.OrdinalIgnoreCase) &&
                (!Input.Id.HasValue || x.Id != Input.Id.Value));

            if (duplicateSlug)
            {
                ModelState.AddModelError(
                    "Input.Slug",
                    "این اسلاگ قبلاً برای برند دیگری ثبت شده است.");
            }

            var duplicateTitle = Brands.Any(x =>
                x.Title.Equals(Input.Title, StringComparison.OrdinalIgnoreCase) &&
                (!Input.Id.HasValue || x.Id != Input.Id.Value));

            if (duplicateTitle)
            {
                ModelState.AddModelError(
                    "Input.Title",
                    "این عنوان قبلاً برای برند دیگری ثبت شده است.");
            }
        }

        private static string? ValidateImageFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > MaxImageSize)
                return "حجم تصویر نباید بیشتر از ۵ مگابایت باشد.";

            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
                return "فرمت تصویر مجاز نیست. فقط jpg، jpeg، png و webp قابل قبول است.";

            var contentType = file.ContentType.ToLowerInvariant();

            if (!AllowedImageContentTypes.Contains(contentType))
                return "نوع فایل انتخاب‌شده معتبر نیست.";

            return null;
        }

        public string GetImagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim();

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            return "/" + path.TrimStart('~', '/');
        }

        public string GetBrandInitial(AdminBrandModel brand)
        {
            if (string.IsNullOrWhiteSpace(brand.Title))
                return "B";

            return brand.Title.Trim()[0].ToString();
        }

        public string GetStatusCss(AdminBrandModel brand)
        {
            return brand.IsActive
                ? "brand-pill success"
                : "brand-pill danger";
        }

        public string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string GenerateSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace("--", "-");
        }
    }
}