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
        public EditModel(ApiClient apiClient) => _apiClient = apiClient;
        [BindProperty] public UpdateProductRequestModel Product {
            get;
            set;
        }
        = new();
        public Guid ProductId {
            get;
            set;
        }
        public List<ProductLookupModel> Categories {
            get;
            set;
        }
        = new();
        public List<ProductLookupModel> Brands {
            get;
            set;
        }
        = new();
        public string? ErrorMessage {
            get;
            set;
        }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            await LoadLookupsAsync();
            ProductId = id;
            var result = await _apiClient.GetAsync<AdminProductModel>("admin/products/" + id);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index");
            }
            Product = new UpdateProductRequestModel
            {
                CategoryId = result.Data.CategoryId,
                BrandId = result.Data.BrandId,
                Title = result.Data.Title,
                Slug = result.Data.Slug,
                ShortDescription = result.Data.ShortDescription,
                FullDescription = result.Data.FullDescription,
                ThumbnailImagePath = result.Data.ThumbnailImagePath,
                ProductType = result.Data.ProductType,
                DeliveryType = result.Data.DeliveryType,
                CurrencyType = result.Data.CurrencyType == 1 ? (byte)1 : (byte)2,
                BasePrice = result.Data.BasePrice,
                DiscountPrice = result.Data.DiscountPrice,
                RequiresVerification = result.Data.RequiresVerification,
                RequiresSupportMessage = result.Data.RequiresSupportMessage,
                MinOrderQuantity = result.Data.MinOrderQuantity,
                MaxOrderQuantity = result.Data.MaxOrderQuantity,
                IsFeatured = result.Data.IsFeatured,
                IsActive = result.Data.IsActive,
                SeoTitle = result.Data.SeoTitle,
                SeoDescription = result.Data.SeoDescription,
                SortOrder = result.Data.SortOrder
            }
            ;
            return Page();
        }
        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            await LoadLookupsAsync();
            if (Product.BrandId == Guid.Empty) Product.BrandId = null;
            if (string.IsNullOrWhiteSpace(Product.Slug)) Product.Slug = GenerateSlug(Product.Title);
            if (Product.DiscountPrice.HasValue && Product.DiscountPrice.Value <= 0) Product.DiscountPrice = null;
            if (Product.DiscountPrice.HasValue && Product.DiscountPrice.Value >= Product.BasePrice)
            ModelState.AddModelError("Product.DiscountPrice", "قیمت تخفیفی باید کمتر از قیمت اصلی باشد.");
            if (!ModelState.IsValid) return Page();
            var result = await _apiClient.PutAsync<AdminProductModel>("admin/products/" + id, Product);
            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }
            TempData["SuccessMessage"] = "محصول با موفقیت ذخیره شد.";
            return RedirectToPage("Details", new {
                id = result.Data.Id }
                );
            }
            private async Task LoadLookupsAsync()
            {
                var categoriesResult = await _apiClient.GetAsync<List<AdminCategoryModel>>("admin/categories");
                if (categoriesResult.IsSuccess && categoriesResult.Data != null)
                Categories = categoriesResult.Data.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).Select(x => new ProductLookupModel {
                    Id = x.Id, Title = x.ParentId.HasValue ? "— " + x.Title : x.Title, Slug = x.Slug }
                    ).ToList();
                    var brandsResult = await _apiClient.GetAsync<List<AdminBrandModel>>("admin/brands");
                    if (brandsResult.IsSuccess && brandsResult.Data != null)
                    Brands = brandsResult.Data.Where(x => x.IsActive).OrderBy(x => x.Title).Select(x => new ProductLookupModel {
                        Id = x.Id, Title = x.Title, Slug = x.Slug }
                        ).ToList();
                    }
                    private static string GenerateSlug(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
                }
            }
