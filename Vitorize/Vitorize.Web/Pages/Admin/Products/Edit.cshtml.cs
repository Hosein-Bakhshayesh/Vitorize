using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize]
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
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("/Admin/Products/Index");
            }

            CurrentProduct = result.Data;

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
                CurrencyType = result.Data.CurrencyType,
                BasePrice = result.Data.BasePrice,
                DiscountPrice = result.Data.DiscountPrice,
                RequiresVerification = result.Data.RequiresVerification,
                RequiresSupportMessage = result.Data.RequiresSupportMessage,
                MinOrderQuantity = result.Data.MinOrderQuantity,
                MaxOrderQuantity = result.Data.MaxOrderQuantity,
                IsFeatured = result.Data.IsFeatured,
                IsActive = result.Data.IsActive,
                SeoTitle = result.Data.SeoTitle,
                SeoDescription = result.Data.SeoDescription
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            ProductId = id;

            await LoadLookupsAsync();

            if (!ModelState.IsValid)
                return Page();

            if (Product.BrandId == Guid.Empty)
                Product.BrandId = null;

            var result = await _apiClient.PutAsync<AdminProductModel>(
                $"admin/products/{id}",
                Product);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            TempData["SuccessMessage"] = "محصول با موفقیت ویرایش شد.";
            return RedirectToPage("/Admin/Products/Details", new { id });
        }

        private async Task LoadLookupsAsync()
        {
            var categoriesResult = await _apiClient.GetAsync<List<ProductLookupModel>>("products/categories");
            if (categoriesResult.IsSuccess && categoriesResult.Data != null)
                Categories = categoriesResult.Data;

            var brandsResult = await _apiClient.GetAsync<List<ProductLookupModel>>("products/brands");
            if (brandsResult.IsSuccess && brandsResult.Data != null)
                Brands = brandsResult.Data;
        }

        public string FormatMoney(decimal amount) => amount.ToString("N0") + " تومان";
    }
}