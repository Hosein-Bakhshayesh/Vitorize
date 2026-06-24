using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Models.Admin.Categories;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Categories
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

        public List<AdminCategoryModel> Categories { get; set; } = new();

        [BindProperty]
        public AdminCategoryInputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        [BindProperty]
        public bool RemoveImage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public int TotalCategories => Categories.Count;

        public int ParentCategoryCount => Categories.Count(x => !x.ParentId.HasValue);

        public int ActiveCategoryCount => Categories.Count(x => x.IsActive);

        public int CategoriesWithImageCount => Categories.Count(x => !string.IsNullOrWhiteSpace(x.ImagePath));

        public List<AdminCategoryModel> ParentCategories =>
            Categories
                .Where(x => x.ParentId == null)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            await LoadAsync();

            NormalizeInput();

            AdminCategoryModel? oldCategory = null;

            if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
            {
                var oldResult = await _apiClient.GetAsync<AdminCategoryModel>(
                    $"admin/categories/{Input.Id.Value}");

                if (oldResult.IsSuccess)
                    oldCategory = oldResult.Data;
            }

            ValidateBusinessRules(oldCategory);

            var imageValidationError = ValidateImageFile(ImageFile);

            if (!string.IsNullOrWhiteSpace(imageValidationError))
            {
                ErrorMessage = imageValidationError;
                return Page();
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "لطفاً خطاهای فرم را بررسی کنید.";
                return Page();
            }

            var newImagePath = string.Empty;
            var oldImagePath = oldCategory?.ImagePath;

            try
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    newImagePath = await _fileStorage.SaveAsync(
                        ImageFile,
                        StorageFolders.Categories) ?? string.Empty;

                    Input.ImagePath = newImagePath;
                }
                else if (RemoveImage)
                {
                    Input.ImagePath = null;
                }
                else if (oldCategory != null)
                {
                    Input.ImagePath = oldCategory.ImagePath;
                }

                var result = Input.Id.HasValue && Input.Id.Value != Guid.Empty
                    ? await _apiClient.PutAsync<AdminCategoryModel>($"admin/categories/{Input.Id.Value}", Input)
                    : await _apiClient.PostAsync<AdminCategoryModel>("admin/categories", Input);

                if (!result.IsSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(newImagePath))
                        await _fileStorage.DeleteAsync(newImagePath);

                    ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                        ? "ذخیره دسته‌بندی با خطا مواجه شد."
                        : result.Message;

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
                    ? "دسته‌بندی با موفقیت ویرایش شد."
                    : "دسته‌بندی با موفقیت ایجاد شد.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newImagePath))
                    await _fileStorage.DeleteAsync(newImagePath);

                ErrorMessage = ex.Message;

                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                ErrorMessage = "شناسه دسته‌بندی معتبر نیست.";
                return RedirectToPage();
            }

            var oldResult = await _apiClient.GetAsync<AdminCategoryModel>(
                $"admin/categories/{id}");

            var result = await _apiClient.DeleteAsync(
                $"admin/categories/{id}");

            if (!result.IsSuccess)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "حذف دسته‌بندی با خطا مواجه شد."
                    : result.Message;

                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(oldResult.Data?.ImagePath))
                await _fileStorage.DeleteAsync(oldResult.Data.ImagePath);

            SuccessMessage = "دسته‌بندی با موفقیت حذف شد.";

            return RedirectToPage();
        }

        public List<AdminCategoryModel> GetChildren(Guid parentId)
        {
            return Categories
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();
        }

        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminCategoryModel>>(
                "admin/categories");

            Categories = result.IsSuccess && result.Data != null
                ? result.Data
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Title)
                    .ToList()
                : new List<AdminCategoryModel>();
        }

        private void NormalizeInput()
        {
            Input.Title = Input.Title?.Trim() ?? string.Empty;

            Input.Slug = string.IsNullOrWhiteSpace(Input.Slug)
                ? GenerateSlug(Input.Title)
                : GenerateSlug(Input.Slug);

            Input.Description = NormalizeNullable(Input.Description);
            Input.ImagePath = NormalizeNullable(Input.ImagePath);
            Input.Icon = NormalizeNullable(Input.Icon);
            Input.SeoTitle = NormalizeNullable(Input.SeoTitle);
            Input.SeoDescription = NormalizeNullable(Input.SeoDescription);

            if (Input.ParentId == Guid.Empty)
                Input.ParentId = null;

            if (Input.SortOrder < 0)
                Input.SortOrder = 0;
        }

        private void ValidateBusinessRules(AdminCategoryModel? oldCategory)
        {
            if (string.IsNullOrWhiteSpace(Input.Title))
            {
                ModelState.AddModelError(
                    "Input.Title",
                    "عنوان دسته‌بندی الزامی است.");
            }

            if (string.IsNullOrWhiteSpace(Input.Slug))
            {
                ModelState.AddModelError(
                    "Input.Slug",
                    "اسلاگ دسته‌بندی الزامی است.");
            }

            if (Input.Id.HasValue &&
                Input.ParentId.HasValue &&
                Input.Id.Value == Input.ParentId.Value)
            {
                ModelState.AddModelError(
                    "Input.ParentId",
                    "دسته‌بندی نمی‌تواند والد خودش باشد.");
            }

            if (Input.ParentId.HasValue &&
                !Categories.Any(x => x.Id == Input.ParentId.Value))
            {
                ModelState.AddModelError(
                    "Input.ParentId",
                    "دسته والد معتبر نیست.");
            }

            if (Input.Id.HasValue &&
                Input.ParentId.HasValue)
            {
                var children = GetChildren(Input.Id.Value);

                if (children.Any(x => x.Id == Input.ParentId.Value))
                {
                    ModelState.AddModelError(
                        "Input.ParentId",
                        "زیرمجموعه نمی‌تواند به عنوان والد انتخاب شود.");
                }
            }

            var duplicateSlug = Categories.Any(x =>
                x.Slug.Equals(Input.Slug, StringComparison.OrdinalIgnoreCase) &&
                (!Input.Id.HasValue || x.Id != Input.Id.Value));

            if (duplicateSlug)
            {
                ModelState.AddModelError(
                    "Input.Slug",
                    "این اسلاگ قبلاً برای دسته‌بندی دیگری ثبت شده است.");
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

        public string GetCategoryIcon(AdminCategoryModel category)
        {
            if (!string.IsNullOrWhiteSpace(category.Icon))
                return category.Icon.Trim();

            if (!string.IsNullOrWhiteSpace(category.Title))
                return category.Title.Trim()[0].ToString();

            return "C";
        }

        public string GetStatusCss(AdminCategoryModel category)
        {
            return category.IsActive
                ? "cat-pill success"
                : "cat-pill danger";
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