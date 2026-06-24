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
            if (productId == Guid.Empty || id == Guid.Empty)
            {
                TempData["ErrorMessage"] = "اطلاعات واریانت معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;
            VariantId = id;

            var loaded = await LoadAsync(productId, id);

            if (!loaded)
                return RedirectToPage("/Admin/ProductVariants/Index", new { productId });

            Variant = MapToUpdateRequest(CurrentVariant!);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid productId, Guid id)
        {
            if (productId == Guid.Empty || id == Guid.Empty)
            {
                TempData["ErrorMessage"] = "اطلاعات واریانت معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;
            VariantId = id;

            await LoadAsync(productId, id);

            NormalizeInput();

            ModelState.Clear();
            TryValidateModel(Variant, nameof(Variant));

            ValidateBusinessRules();

            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PutAsync<AdminProductVariantModel>(
                $"admin/product-variants/{id}",
                Variant);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "ذخیره تغییرات واریانت با خطا مواجه شد."
                    : result.Message;

                return Page();
            }

            TempData["SuccessMessage"] = "واریانت محصول با موفقیت ویرایش شد.";

            return RedirectToPage("/Admin/ProductVariants/Index", new { productId });
        }

        private async Task<bool> LoadAsync(Guid productId, Guid id)
        {
            var productResult = await _apiClient.GetAsync<AdminProductModel>(
                $"admin/products/{productId}");

            if (productResult.IsSuccess && productResult.Data != null)
            {
                Product = productResult.Data;
            }

            var variantResult = await _apiClient.GetAsync<AdminProductVariantModel>(
                $"admin/product-variants/{id}");

            if (!variantResult.IsSuccess || variantResult.Data == null)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(variantResult.Message)
                    ? "واریانت مورد نظر پیدا نشد."
                    : variantResult.Message;

                return false;
            }

            CurrentVariant = variantResult.Data;

            return true;
        }

        private static UpdateProductVariantRequestModel MapToUpdateRequest(
            AdminProductVariantModel variant)
        {
            return new UpdateProductVariantRequestModel
            {
                Title = variant.Title,
                Sku = variant.Sku,
                Price = variant.Price,
                DiscountPrice = variant.DiscountPrice,
                Value = variant.Value,
                StockMode = variant.StockMode,
                IsDefault = variant.IsDefault,
                IsActive = variant.IsActive,
                SortOrder = variant.SortOrder
            };
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
    }
}