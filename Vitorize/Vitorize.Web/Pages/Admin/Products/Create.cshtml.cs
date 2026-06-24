using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Brands;
using Vitorize.Web.Models.Admin.Categories;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class CreateModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public CreateModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [BindProperty]
        public CreateProductRequestModel Product { get; set; } = new();

        public List<ProductLookupModel> Categories { get; set; } = new();

        public List<ProductLookupModel> Brands { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Product.IsActive = true;
            Product.MinOrderQuantity = 1;
            Product.ProductType = 1;
            Product.DeliveryType = 1;
            Product.CurrencyType = 2;

            await LoadLookupsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadLookupsAsync();

            NormalizeInput();

            ModelState.Clear();
            TryValidateModel(Product, nameof(Product));

            ValidateBusinessRules();

            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PostAsync<AdminProductModel>(
                "admin/products",
                Product);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "ایجاد محصول با خطا مواجه شد."
                    : result.Message;

                return Page();
            }

            TempData["SuccessMessage"] = "محصول با موفقیت ایجاد شد. حالا تصاویر محصول را اضافه کن.";

            return RedirectToPage("/Admin/ProductImages/Index", new
            {
                productId = result.Data.Id
            });
        }

        private void NormalizeInput()
        {
            Product.Title = Product.Title?.Trim() ?? string.Empty;

            Product.Slug = string.IsNullOrWhiteSpace(Product.Slug)
                ? GenerateSlug(Product.Title)
                : GenerateSlug(Product.Slug);

            Product.ShortDescription = NormalizeNullable(Product.ShortDescription);
            Product.FullDescription = NormalizeNullable(Product.FullDescription);
            Product.SeoTitle = NormalizeNullable(Product.SeoTitle);
            Product.SeoDescription = NormalizeNullable(Product.SeoDescription);
            Product.ThumbnailImagePath = NormalizeNullable(Product.ThumbnailImagePath);

            if (Product.BrandId == Guid.Empty)
                Product.BrandId = null;

            if (Product.MinOrderQuantity <= 0)
                Product.MinOrderQuantity = 1;

            if (Product.DiscountPrice.HasValue && Product.DiscountPrice.Value <= 0)
                Product.DiscountPrice = null;
        }

        private void ValidateBusinessRules()
        {
            if (Product.CategoryId == Guid.Empty)
            {
                ModelState.AddModelError(
                    "Product.CategoryId",
                    "انتخاب دسته‌بندی الزامی است.");
            }

            if (Product.BasePrice < 0)
            {
                ModelState.AddModelError(
                    "Product.BasePrice",
                    "قیمت پایه نمی‌تواند منفی باشد.");
            }

            if (Product.DiscountPrice.HasValue &&
                Product.DiscountPrice.Value >= Product.BasePrice &&
                Product.BasePrice > 0)
            {
                ModelState.AddModelError(
                    "Product.DiscountPrice",
                    "قیمت تخفیف باید کمتر از قیمت پایه باشد.");
            }

            if (Product.MaxOrderQuantity.HasValue &&
                Product.MaxOrderQuantity.Value < Product.MinOrderQuantity)
            {
                ModelState.AddModelError(
                    "Product.MaxOrderQuantity",
                    "حداکثر تعداد سفارش نمی‌تواند کمتر از حداقل تعداد سفارش باشد.");
            }
        }

        private async Task LoadLookupsAsync()
        {
            Categories = new List<ProductLookupModel>();
            Brands = new List<ProductLookupModel>();

            var categoriesResult = await _apiClient.GetAsync<List<AdminCategoryModel>>("admin/categories");

            if (categoriesResult.IsSuccess && categoriesResult.Data != null)
            {
                Categories = categoriesResult.Data
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Title)
                    .Select(x => new ProductLookupModel
                    {
                        Id = x.Id,
                        Title = x.ParentId.HasValue
                            ? "— " + x.Title
                            : x.Title,
                        Slug = x.Slug
                    })
                    .ToList();
            }

            var brandsResult = await _apiClient.GetAsync<List<AdminBrandModel>>("admin/brands");

            if (brandsResult.IsSuccess && brandsResult.Data != null)
            {
                Brands = brandsResult.Data
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Title)
                    .Select(x => new ProductLookupModel
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Slug = x.Slug
                    })
                    .ToList();
            }
        }

        private static string NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string GenerateSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace("--", "-");
        }
    }
}