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
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public List<AdminProductModel> AllProducts { get; set; } = new();

        public List<AdminProductModel> Products { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public int TotalProducts => AllProducts.Count;

        public int ActiveProducts => AllProducts.Count(x => x.IsActive);

        public int FeaturedProducts => AllProducts.Count(x => x.IsFeatured);

        public int OutOfStockProducts => AllProducts.Count(x => x.AvailableStock <= 0);

        public int DiscountedProducts => AllProducts.Count(HasDiscount);

        public int TotalAvailableStock => AllProducts.Sum(x => Math.Max(0, x.AvailableStock));

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(Status) ||
            !string.IsNullOrWhiteSpace(Sort);

        public async Task OnGetAsync()
        {
            await LoadProductsAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync($"admin/products/{id}");

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "محصول با موفقیت حذف شد."
                    : string.IsNullOrWhiteSpace(result.Message)
                        ? "حذف محصول با خطا مواجه شد."
                        : result.Message;

            return RedirectToPage(new
            {
                search = Search,
                status = Status,
                sort = Sort
            });
        }

        private async Task LoadProductsAsync()
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

            var result = await _apiClient.GetAsync<List<AdminProductModel>>("admin/products");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "امکان دریافت لیست محصولات وجود ندارد."
                    : result.Message;

                Products = new List<AdminProductModel>();
                AllProducts = new List<AdminProductModel>();
                return;
            }

            AllProducts = result.Data
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var query = AllProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var keyword = Search.Trim();

                query = query.Where(x =>
                    Contains(x.Title, keyword) ||
                    Contains(x.Slug, keyword) ||
                    Contains(x.CategoryTitle, keyword) ||
                    Contains(x.BrandTitle, keyword));
            }

            query = Status switch
            {
                "active" => query.Where(x => x.IsActive),
                "inactive" => query.Where(x => !x.IsActive),
                "featured" => query.Where(x => x.IsFeatured),
                "out-stock" => query.Where(x => x.AvailableStock <= 0),
                "discounted" => query.Where(HasDiscount),
                "instant" => query.Where(x => x.DeliveryType == (byte)DeliveryType.Instant),
                "verification" => query.Where(x => x.RequiresVerification),
                _ => query
            };

            query = Sort switch
            {
                "oldest" => query.OrderBy(x => x.CreatedAt),
                "title" => query.OrderBy(x => x.Title),
                "price-desc" => query.OrderByDescending(x => x.FinalPrice),
                "price-asc" => query.OrderBy(x => x.FinalPrice),
                "stock-desc" => query.OrderByDescending(x => x.AvailableStock),
                "stock-asc" => query.OrderBy(x => x.AvailableStock),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            Products = query.ToList();
        }

        private static bool Contains(string? source, string keyword)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
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

        public string GetStatusText(AdminProductModel product)
        {
            if (!product.IsActive)
                return "غیرفعال";

            if (product.AvailableStock <= 0)
                return "ناموجود";

            return "فعال";
        }

        public string GetStatusCss(AdminProductModel product)
        {
            if (!product.IsActive)
                return "product-pill danger";

            if (product.AvailableStock <= 0)
                return "product-pill warning";

            return "product-pill success";
        }

        public string GetDeliveryCss(AdminProductModel product)
        {
            return product.DeliveryType switch
            {
                (byte)DeliveryType.Instant => "product-pill blue",
                (byte)DeliveryType.Manual => "product-pill purple",
                (byte)DeliveryType.SupportRequired => "product-pill amber",
                _ => "product-pill muted"
            };
        }

        public string GetStockCss(AdminProductModel product)
        {
            if (product.AvailableStock <= 0)
                return "stock-value danger";

            if (product.AvailableStock <= 5)
                return "stock-value warning";

            return "stock-value success";
        }

        public string GetStockText(AdminProductModel product)
        {
            if (product.AvailableStock <= 0)
                return "نیازمند تامین";

            if (product.AvailableStock <= 5)
                return "موجودی کم";

            return "موجودی مناسب";
        }

        public bool HasDiscount(AdminProductModel product)
        {
            return product.DiscountPrice.HasValue &&
                   product.DiscountPrice.Value > 0 &&
                   product.DiscountPrice.Value < product.BasePrice;
        }

        public int GetDiscountPercent(AdminProductModel product)
        {
            if (!HasDiscount(product) || product.BasePrice <= 0)
                return 0;

            var percent = (product.BasePrice - product.DiscountPrice!.Value) / product.BasePrice * 100;

            return (int)Math.Round(percent);
        }

        public string GetProductInitial(AdminProductModel product)
        {
            if (string.IsNullOrWhiteSpace(product.Title))
                return "V";

            return product.Title.Trim()[0].ToString();
        }

        public string GetImagePath(AdminProductModel product)
        {
            if (string.IsNullOrWhiteSpace(product.ThumbnailImagePath))
                return string.Empty;

            var path = product.ThumbnailImagePath.Trim();

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            return "/" + path.TrimStart('~', '/');
        }
    }
}