using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Products.Variants
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public Guid ProductId { get; set; }

        public AdminProductModel? Product { get; set; }

        public List<AdminProductVariantModel> Variants { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public int TotalVariants => Variants.Count;

        public int ActiveVariants => Variants.Count(x => x.IsActive);

        public int TotalAvailableStock => Variants.Sum(x => x.AvailableStock);

        public int OutOfStockVariants => Variants.Count(x => x.AvailableStock <= 0);

        public async Task<IActionResult> OnGetAsync(Guid productId)
        {
            ProductId = productId;
            await LoadAsync(productId);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid productId, Guid id)
        {
            var result = await _apiClient.DeleteAsync($"admin/product-variants/{id}");

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "تنوع محصول با موفقیت حذف شد."
                    : result.Message;

            return RedirectToPage(new { productId });
        }

        private async Task LoadAsync(Guid productId)
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

            var productResult = await _apiClient.GetAsync<AdminProductModel>(
                $"admin/products/{productId}");

            if (productResult.IsSuccess && productResult.Data != null)
            {
                Product = productResult.Data;
            }
            else
            {
                ErrorMessage = productResult.Message;
            }

            var variantsResult = await _apiClient.GetAsync<List<AdminProductVariantModel>>(
                $"admin/products/{productId}/variants");

            if (variantsResult.IsSuccess && variantsResult.Data != null)
            {
                Variants = variantsResult.Data
                    .OrderByDescending(x => x.IsDefault)
                    .ThenBy(x => x.SortOrder)
                    .ThenBy(x => x.Title)
                    .ToList();
            }
            else
            {
                ErrorMessage = variantsResult.Message;
            }
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string GetStockMode(byte stockMode)
        {
            return stockMode switch
            {
                1 => "کد گیفت کارت",
                2 => "موجودی دستی",
                3 => "بدون محدودیت",
                _ => "نامشخص"
            };
        }
    }
}