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
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public EditModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [BindProperty]
        public UpdateProductRequestModel Product { get; set; } = new();

        public Guid ProductId { get; set; }

        public AdminProductModel? CurrentProduct { get; set; }

        public List<ProductLookupModel> Categories { get; set; } = new();

        public List<ProductLookupModel> Brands { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            ProductId = id;

            await LoadLookupsAsync();

            var result = await _apiClient.GetAsync<AdminProductModel>($"admin/products/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(result.Message)
                    ? "محصول مورد نظر پیدا نشد."
                    : result.Message;

                return RedirectToPage("/Admin/Products/Index");
            }

            CurrentProduct = result.Data;
            Product = MapToUpdateRequest(result.Data);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            ProductId = id;

            await LoadLookupsAsync();

            NormalizeInput();

            ModelState.Clear();
            TryValidateModel(Product, nameof(Product));

            ValidateBusinessRules();

            if (!ModelState.IsValid)
            {
                await LoadCurrentProductAsync(id);
                return Page();
            }

            var result = await _apiClient.PutAsync<AdminProductModel>(
                $"admin/products/{id}",
                Product);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "ذخیره تغییرات محصول با خطا مواجه شد."
                    : result.Message;

                await LoadCurrentProductAsync(id);
                return Page();
            }

            TempData["SuccessMessage"] = "محصول با موفقیت ویرایش شد.";

            return RedirectToPage("/Admin/Products/Details", new { id });
        }

        private static UpdateProductRequestModel MapToUpdateRequest(AdminProductModel product)
        {
            return new UpdateProductRequestModel
            {
                CategoryId = product.CategoryId,
                BrandId = product.BrandId,
                Title = product.Title,
                Slug = product.Slug,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                ThumbnailImagePath = product.ThumbnailImagePath,
                ProductType = product.ProductType,
                DeliveryType = product.DeliveryType,
                CurrencyType = product.CurrencyType,
                BasePrice = product.BasePrice,
                DiscountPrice = product.DiscountPrice,
                RequiresVerification = product.RequiresVerification,
                RequiresSupportMessage = product.RequiresSupportMessage,
                MinOrderQuantity = product.MinOrderQuantity,
                MaxOrderQuantity = product.MaxOrderQuantity,
                IsFeatured = product.IsFeatured,
                IsActive = product.IsActive,
                SeoTitle = product.SeoTitle,
                SeoDescription = product.SeoDescription
            };
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

        private async Task LoadCurrentProductAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminProductModel>($"admin/products/{id}");

            if (result.IsSuccess && result.Data != null)
                CurrentProduct = result.Data;
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

        public string GetImagePath()
        {
            var path = Product.ThumbnailImagePath;

            if (string.IsNullOrWhiteSpace(path))
                path = CurrentProduct?.ThumbnailImagePath;

            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim();

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            return "/" + path.TrimStart('~', '/');
        }

        public string GetProductInitial()
        {
            if (!string.IsNullOrWhiteSpace(Product.Title))
                return Product.Title.Trim()[0].ToString();

            if (!string.IsNullOrWhiteSpace(CurrentProduct?.Title))
                return CurrentProduct.Title.Trim()[0].ToString();

            return "V";
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