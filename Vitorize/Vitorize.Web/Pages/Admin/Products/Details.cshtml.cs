using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Shared.Enums;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public DetailsModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public AdminProductModel Product { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            ErrorMessage = TempData["ErrorMessage"]?.ToString();

            var result = await _apiClient.GetAsync<AdminProductModel>($"admin/products/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(result.Message)
                    ? "محصول مورد نظر پیدا نشد."
                    : result.Message;

                return RedirectToPage("/Admin/Products/Index");
            }

            Product = result.Data;

            return Page();
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

        public string GetProductType(byte type)
        {
            return ((ProductType)type) switch
            {
                ProductType.GiftCard => "گیفت کارت",
                ProductType.GameAccount => "اکانت بازی",
                ProductType.GameService => "سرویس بازی",
                ProductType.Subscription => "اشتراک",
                ProductType.Other => "سایر",
                _ => "نامشخص"
            };
        }

        public string GetDeliveryType(byte type)
        {
            return ((DeliveryType)type) switch
            {
                DeliveryType.Instant => "تحویل آنی",
                DeliveryType.Manual => "تحویل دستی",
                DeliveryType.SupportRequired => "نیازمند پشتیبانی",
                _ => "نامشخص"
            };
        }

        public string GetCurrencyType(byte type)
        {
            return ((CurrencyType)type) switch
            {
                CurrencyType.Rial => "ریال",
                CurrencyType.Toman => "تومان",
                CurrencyType.USD => "دلار",
                _ => "نامشخص"
            };
        }

        public string GetStatusText()
        {
            if (!Product.IsActive)
                return "غیرفعال";

            if (Product.AvailableStock <= 0)
                return "ناموجود";

            return "فعال";
        }

        public string GetStatusCss()
        {
            if (!Product.IsActive)
                return "pd-pill danger";

            if (Product.AvailableStock <= 0)
                return "pd-pill warning";

            return "pd-pill success";
        }

        public string GetStockCss()
        {
            if (Product.AvailableStock <= 0)
                return "danger";

            if (Product.AvailableStock <= 5)
                return "warning";

            return "success";
        }

        public string GetStockText()
        {
            if (Product.AvailableStock <= 0)
                return "نیازمند تامین";

            if (Product.AvailableStock <= 5)
                return "موجودی کم";

            return "موجودی مناسب";
        }

        public bool HasDiscount()
        {
            return Product.DiscountPrice.HasValue &&
                   Product.DiscountPrice.Value > 0 &&
                   Product.DiscountPrice.Value < Product.BasePrice;
        }

        public int GetDiscountPercent()
        {
            if (!HasDiscount() || Product.BasePrice <= 0)
                return 0;

            var percent = (Product.BasePrice - Product.DiscountPrice!.Value) / Product.BasePrice * 100;

            return (int)Math.Round(percent);
        }

        public string GetProductInitial()
        {
            if (string.IsNullOrWhiteSpace(Product.Title))
                return "V";

            return Product.Title.Trim()[0].ToString();
        }

        public string GetImagePath()
        {
            if (string.IsNullOrWhiteSpace(Product.ThumbnailImagePath))
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