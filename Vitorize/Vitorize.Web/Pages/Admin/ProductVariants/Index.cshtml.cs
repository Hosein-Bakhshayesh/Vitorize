using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Shared.Enums;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.ProductVariants
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
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

        public int TotalAvailableStock => Variants.Sum(x => Math.Max(0, x.AvailableStock));

        public int OutOfStockVariants => Variants.Count(x => x.AvailableStock <= 0);

        public AdminProductVariantModel? DefaultVariant =>
            Variants.FirstOrDefault(x => x.IsDefault);

        public async Task<IActionResult> OnGetAsync(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "شناسه محصول معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;

            var loaded = await LoadAsync(productId);

            if (!loaded)
                return RedirectToPage("/Admin/Products/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid productId, Guid id)
        {
            if (productId == Guid.Empty || id == Guid.Empty)
            {
                TempData["ErrorMessage"] = "اطلاعات واریانت معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            var result = await _apiClient.DeleteAsync($"admin/product-variants/{id}");

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "واریانت محصول با موفقیت حذف شد."
                    : string.IsNullOrWhiteSpace(result.Message)
                        ? "حذف واریانت با خطا مواجه شد."
                        : result.Message;

            return RedirectToPage(new { productId });
        }

        private async Task<bool> LoadAsync(Guid productId)
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

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
                Variants = new List<AdminProductVariantModel>();

                if (!string.IsNullOrWhiteSpace(variantsResult.Message))
                    ErrorMessage = variantsResult.Message;
            }

            return true;
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        public string FormatDate(DateTime date)
        {
            return date.ToString("yyyy/MM/dd");
        }

        public string GetStockMode(byte stockMode)
        {
            return ((ProductVariantStockMode)stockMode) switch
            {
                ProductVariantStockMode.GiftCode => "بر اساس گیفت‌کد",
                ProductVariantStockMode.Manual => "موجودی دستی",
                ProductVariantStockMode.Unlimited => "بدون محدودیت",
                _ => "نامشخص"
            };
        }

        public string GetStockModeCss(byte stockMode)
        {
            return ((ProductVariantStockMode)stockMode) switch
            {
                ProductVariantStockMode.GiftCode => "variant-pill blue",
                ProductVariantStockMode.Manual => "variant-pill purple",
                ProductVariantStockMode.Unlimited => "variant-pill amber",
                _ => "variant-pill muted"
            };
        }

        public string GetStatusText(AdminProductVariantModel variant)
        {
            if (!variant.IsActive)
                return "غیرفعال";

            if (variant.AvailableStock <= 0 &&
                variant.StockMode != (byte)ProductVariantStockMode.Unlimited)
                return "ناموجود";

            return "فعال";
        }

        public string GetStatusCss(AdminProductVariantModel variant)
        {
            if (!variant.IsActive)
                return "variant-pill danger";

            if (variant.AvailableStock <= 0 &&
                variant.StockMode != (byte)ProductVariantStockMode.Unlimited)
                return "variant-pill warning";

            return "variant-pill success";
        }

        public string GetStockCss(AdminProductVariantModel variant)
        {
            if (variant.StockMode == (byte)ProductVariantStockMode.Unlimited)
                return "stock-value success";

            if (variant.AvailableStock <= 0)
                return "stock-value danger";

            if (variant.AvailableStock <= 5)
                return "stock-value warning";

            return "stock-value success";
        }

        public string GetStockText(AdminProductVariantModel variant)
        {
            if (variant.StockMode == (byte)ProductVariantStockMode.Unlimited)
                return "بدون محدودیت";

            if (variant.AvailableStock <= 0)
                return "نیازمند تامین";

            if (variant.AvailableStock <= 5)
                return "موجودی کم";

            return "موجودی مناسب";
        }

        public bool HasDiscount(AdminProductVariantModel variant)
        {
            return variant.DiscountPrice.HasValue &&
                   variant.DiscountPrice.Value > 0 &&
                   variant.DiscountPrice.Value < variant.Price;
        }

        public string GetProductInitial()
        {
            if (string.IsNullOrWhiteSpace(Product?.Title))
                return "V";

            return Product.Title.Trim()[0].ToString();
        }

        public string GetProductImagePath()
        {
            if (string.IsNullOrWhiteSpace(Product?.ThumbnailImagePath))
                return string.Empty;

            var path = Product.ThumbnailImagePath.Trim();

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            return "/" + path.TrimStart('~', '/');
        }
    }
}