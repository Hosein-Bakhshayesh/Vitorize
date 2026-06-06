using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize]
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
            await LoadLookupsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadLookupsAsync();

            if (!ModelState.IsValid)
                return Page();

            if (Product.BrandId == Guid.Empty)
                Product.BrandId = null;

            var result = await _apiClient.PostAsync<AdminProductModel>(
                "admin/products",
                Product);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            TempData["SuccessMessage"] = "محصول ساخته شد. حالا تصاویر محصول را اضافه کن.";

            return RedirectToPage("/Admin/Products/Images/Index", new
            {
                productId = result.Data.Id
            });
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
    }
}