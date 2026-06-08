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
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme)]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorage;

        public IndexModel(ApiClient apiClient, IFileStorageService fileStorage)
        {
            _apiClient = apiClient;
            _fileStorage = fileStorage;
        }

        public List<AdminCategoryModel> Categories { get; set; } = new();

        [BindProperty]
        public AdminCategoryInputModel Input { get; set; } = new();

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
                ErrorMessage = "عنوان دسته‌بندی الزامی است.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(Input.Slug))
                Input.Slug = GenerateSlug(Input.Title);

            if (Input.ParentId == Guid.Empty)
                Input.ParentId = null;

            AdminCategoryModel? oldCategory = null;

            if (Input.Id.HasValue && Input.Id.Value != Guid.Empty)
            {
                var oldResult = await _apiClient.GetAsync<AdminCategoryModel>(
                    $"admin/categories/{Input.Id.Value}");

                if (oldResult.IsSuccess)
                    oldCategory = oldResult.Data;
            }

            var uploadedImagePath = await _fileStorage.SaveAsync(
                ImageFile,
                StorageFolders.Categories);

            if (!string.IsNullOrWhiteSpace(uploadedImagePath))
            {
                if (!string.IsNullOrWhiteSpace(oldCategory?.ImagePath))
                    await _fileStorage.DeleteAsync(oldCategory.ImagePath);

                Input.ImagePath = uploadedImagePath;
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
                ErrorMessage = result.Message;
                return RedirectToPage();
            }

            SuccessMessage = Input.Id.HasValue && Input.Id.Value != Guid.Empty
                ? "دسته‌بندی با موفقیت ویرایش شد."
                : "دسته‌بندی با موفقیت ایجاد شد.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var oldResult = await _apiClient.GetAsync<AdminCategoryModel>($"admin/categories/{id}");

            var result = await _apiClient.DeleteAsync($"admin/categories/{id}");

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message;
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(oldResult.Data?.ImagePath))
                await _fileStorage.DeleteAsync(oldResult.Data.ImagePath);

            SuccessMessage = "دسته‌بندی با موفقیت حذف شد.";
            return RedirectToPage();
        }

        public List<AdminCategoryModel> ParentCategories =>
            Categories
                .Where(x => x.ParentId == null)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .ToList();

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
            var result = await _apiClient.GetAsync<List<AdminCategoryModel>>("admin/categories");

            Categories = result.IsSuccess && result.Data != null
                ? result.Data
                : new List<AdminCategoryModel>();
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