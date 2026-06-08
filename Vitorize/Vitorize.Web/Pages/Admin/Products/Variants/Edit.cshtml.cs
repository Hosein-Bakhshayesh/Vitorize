using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Products.Variants
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme)]
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public EditModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public Guid ProductId { get; set; }

        public Guid VariantId { get; set; }

        public AdminProductModel? Product { get; set; }

        public AdminProductVariantModel? CurrentVariant { get; set; }

        [BindProperty]
        public UpdateProductVariantRequestModel Variant { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid productId, Guid id)
        {
            ProductId = productId;
            VariantId = id;

            await LoadProductAsync(productId);

            var result = await _apiClient.GetAsync<AdminProductVariantModel>(
                $"admin/product-variants/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("/Admin/Products/Variants/Index", new { productId });
            }

            CurrentVariant = result.Data;

            Variant = new UpdateProductVariantRequestModel
            {
                Title = result.Data.Title,
                Sku = result.Data.Sku,
                Price = result.Data.Price,
                DiscountPrice = result.Data.DiscountPrice,
                Value = result.Data.Value,
                StockMode = result.Data.StockMode,
                IsDefault = result.Data.IsDefault,
                IsActive = result.Data.IsActive,
                SortOrder = result.Data.SortOrder
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid productId, Guid id)
        {
            ProductId = productId;
            VariantId = id;

            await LoadProductAsync(productId);

            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PutAsync<AdminProductVariantModel>(
                $"admin/product-variants/{id}",
                Variant);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            TempData["SuccessMessage"] = "تنوع محصول با موفقیت ویرایش شد.";
            return RedirectToPage("/Admin/Products/Variants/Index", new { productId });
        }

        private async Task LoadProductAsync(Guid productId)
        {
            var productResult = await _apiClient.GetAsync<AdminProductModel>(
                $"admin/products/{productId}");

            if (productResult.IsSuccess && productResult.Data != null)
                Product = productResult.Data;
            else
                ErrorMessage = productResult.Message;
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }
    }
}