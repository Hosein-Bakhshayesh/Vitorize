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
        public CreateModel(ApiClient apiClient) => _apiClient = apiClient;
        [BindProperty] public CreateProductRequestModel Product {
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
        public async Task<IActionResult> OnGetAsync()
        {
            await LoadLookupsAsync();
            Product.IsActive = true;
            Product.MinOrderQuantity = 1;
            Product.ProductType = 1;
            Product.DeliveryType = 1;
            Product.CurrencyType = 2;
            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            await LoadLookupsAsync();
            if (Product.BrandId == Guid.Empty) Product.BrandId = null;
            if (string.IsNullOrWhiteSpace(Product.Slug)) Product.Slug = GenerateSlug(Product.Title);
            if (Product.DiscountPrice.HasValue && Product.DiscountPrice.Value <= 0) Product.DiscountPrice = null;
            if (Product.DiscountPrice.HasValue && Product.DiscountPrice.Value >= Product.BasePrice)
            ModelState.AddModelError("Product.DiscountPrice", "قیمت تخفیفی باید کمتر از قیمت اصلی باشد.");
            if (!ModelState.IsValid) return Page();
            var result = await _apiClient.PostAsync<AdminProductModel>("admin/products", Product);
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
