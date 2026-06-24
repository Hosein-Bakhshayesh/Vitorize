using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.ProductVariants
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
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
            if (productId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "شناسه محصول معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;

            var productLoaded = await LoadProductAsync(productId);

            if (!productLoaded)
                return RedirectToPage("/Admin/Products/Index");

            Variant.StockMode = 1;
            Variant.IsActive = true;
            Variant.IsDefault = false;
            Variant.SortOrder = await ResolveNextSortOrderAsync(productId);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "شناسه محصول معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;

            var productLoaded = await LoadProductAsync(productId);

            if (!productLoaded)
                return RedirectToPage("/Admin/Products/Index");

            NormalizeInput();

            ModelState.Clear();
            TryValidateModel(Variant, nameof(Variant));

            ValidateBusinessRules();

            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PostAsync<AdminProductVariantModel>(
                $"admin/products/{productId}/variants",
                Variant);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "ایجاد واریانت محصول با خطا مواجه شد."
                    : result.Message;

                return Page();
            }

            TempData["SuccessMessage"] = "واریانت محصول با موفقیت ایجاد شد.";

            return RedirectToPage("/Admin/ProductVariants/Index", new { productId });
        }

        private async Task<bool> LoadProductAsync(Guid productId)
        {
            var productResult = await _apiClient.GetAsync<AdminProductModel>(
                $"admin/products/{productId}");

            if (!productResult.IsSuccess || productResult.Data == null)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(productResult.Message)
                    ? "محصول مورد نظر پیدا نشد."
                    : productResult.Message;

                return false;
            }

            Product = productResult.Data;

            return true;
        }

        private async Task<int> ResolveNextSortOrderAsync(Guid productId)
        {
            var result = await _apiClient.GetAsync<List<AdminProductVariantModel>>(
                $"admin/products/{productId}/variants");

            if (!result.IsSuccess || result.Data == null || !result.Data.Any())
                return 10;

            return result.Data.Max(x => x.SortOrder) + 10;
        }

        private void NormalizeInput()
        {
            Variant.Title = Variant.Title?.Trim() ?? string.Empty;
            Variant.Sku = NormalizeNullable(Variant.Sku);
            Variant.Value = NormalizeNullable(Variant.Value);

            if (Variant.SortOrder < 0)
                Variant.SortOrder = 0;

            if (Variant.DiscountPrice.HasValue && Variant.DiscountPrice.Value <= 0)
                Variant.DiscountPrice = null;
        }

        private void ValidateBusinessRules()
        {
            if (Variant.Price < 0)
            {
                ModelState.AddModelError(
                    "Variant.Price",
                    "قیمت نمی‌تواند منفی باشد.");
            }

            if (Variant.DiscountPrice.HasValue &&
                Variant.DiscountPrice.Value >= Variant.Price &&
                Variant.Price > 0)
            {
                ModelState.AddModelError(
                    "Variant.DiscountPrice",
                    "قیمت تخفیف باید کمتر از قیمت اصلی باشد.");
            }

            if (Variant.StockMode < 1 || Variant.StockMode > 3)
            {
                ModelState.AddModelError(
                    "Variant.StockMode",
                    "مدل موجودی معتبر نیست.");
            }

            if (Variant.IsDefault && !Variant.IsActive)
            {
                ModelState.AddModelError(
                    "Variant.IsDefault",
                    "واریانت غیرفعال نمی‌تواند پیش‌فرض باشد.");
            }
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}