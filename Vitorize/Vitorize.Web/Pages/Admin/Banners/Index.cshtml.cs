using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Banners;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Banners
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

        public List<AdminBannerModel> Items { get; set; } = new();

        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(SupportsGet = true)] public string? Position { get; set; }
        [BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public int TotalCount => Items.Count;
        public int ActiveCount => Items.Count(x => x.IsActive);
        public int InactiveCount => Items.Count(x => !x.IsActive);
        public int ScheduledCount => Items.Count(x => x.StartsAt.HasValue || x.EndsAt.HasValue);

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var oldResult = await _apiClient.GetAsync<AdminBannerModel>($"admin/banners/{id}");
            var result = await _apiClient.DeleteAsync($"admin/banners/{id}");

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage();
            }

            if (oldResult.IsSuccess && oldResult.Data != null)
            {
                await _fileStorage.DeleteAsync(oldResult.Data.ImagePath);
                await _fileStorage.DeleteAsync(oldResult.Data.MobileImagePath);
            }

            TempData["SuccessMessage"] = "بنر با موفقیت حذف شد.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminBannerModel>>("admin/banners");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                Items = new List<AdminBannerModel>();
                return;
            }

            Items = result.Data
                .OrderBy(x => x.Position)
                .ThenBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedAt)
                .ToList();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                Items = Items.Where(x =>
                    x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    (x.LinkUrl?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    x.Position.Contains(Search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(Position))
                Items = Items.Where(x => x.Position == Position).ToList();

            if (IsActive.HasValue)
                Items = Items.Where(x => x.IsActive == IsActive.Value).ToList();
        }

        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
        public string Date(DateTime? value) => AdminUiHelper.Date(value);
        public string PositionTitle(string? value) => BannerPositions.Title(value);
        public List<AdminBannerPositionOption> Positions => BannerPositions.All;
    }

    public static class BannerPositions
    {
        public static List<AdminBannerPositionOption> All { get; } = new()
        {
            new("home-hero", "اسلایدر اصلی خانه"),
            new("home-top", "بالای صفحه خانه"),
            new("home-middle", "میانه صفحه خانه"),
            new("home-bottom", "پایین صفحه خانه"),
            new("category-strip", "بخش دسته‌بندی‌ها"),
            new("promotion", "کمپین و پیشنهاد ویژه")
        };

        public static string Title(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "نامشخص";

            return All.FirstOrDefault(x => x.Value == value)?.Title ?? value;
        }
    }
}
