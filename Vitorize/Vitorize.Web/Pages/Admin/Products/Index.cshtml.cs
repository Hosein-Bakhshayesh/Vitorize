using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public List<AdminProductModel> Products { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public int TotalProducts => Products.Count;
        public int ActiveProducts => Products.Count(x => x.IsActive);
        public int FeaturedProducts => Products.Count(x => x.IsFeatured);
        public int OutOfStockProducts => Products.Count(x => x.AvailableStock <= 0);

        public async Task OnGetAsync()
        {
            await LoadProductsAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync($"admin/products/{id}");

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess ? "محصول با موفقیت حذف شد." : result.Message;

            return RedirectToPage();
        }

        private async Task LoadProductsAsync()
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

            var result = await _apiClient.GetAsync<List<AdminProductModel>>("admin/products");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return;
            }

            Products = result.Data;

            if (!string.IsNullOrWhiteSpace(Search))
            {
                Products = Products
                    .Where(x =>
                        x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                        x.Slug.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                        x.CategoryTitle.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                        (x.BrandTitle?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            Products = Status switch
            {
                "active" => Products.Where(x => x.IsActive).ToList(),
                "inactive" => Products.Where(x => !x.IsActive).ToList(),
                "featured" => Products.Where(x => x.IsFeatured).ToList(),
                "out-stock" => Products.Where(x => x.AvailableStock <= 0).ToList(),
                _ => Products
            };
        }

        public string FormatMoney(decimal amount) => amount.ToString("N0") + " تومان";

        public string GetProductType(byte type) => type switch
        {
            1 => "گیفت کارت",
            2 => "اکانت بازی",
            3 => "سرویس دیجیتال",
            _ => "نامشخص"
        };

        public string GetDeliveryType(byte type) => type switch
        {
            1 => "تحویل آنی",
            2 => "تحویل دستی",
            3 => "تیکتی",
            _ => "نامشخص"
        };
    }
}