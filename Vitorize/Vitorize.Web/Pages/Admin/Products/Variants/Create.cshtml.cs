using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Products.Variants
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public CreateModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public Guid ProductId { get; set; }

        public AdminProductModel? Product { get; set; }

        [BindProperty]
        public CreateProductVariantRequestModel Variant { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid productId)
        {
            ProductId = productId;
            await LoadProductAsync(productId);

            Variant.StockMode = 1;
            Variant.IsActive = true;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid productId)
        {
            ProductId = productId;
            await LoadProductAsync(productId);

            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PostAsync<AdminProductVariantModel>(
                $"admin/products/{productId}/variants",
                Variant);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            TempData["SuccessMessage"] = "تنوع محصول با موفقیت ایجاد شد.";
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
    }
}