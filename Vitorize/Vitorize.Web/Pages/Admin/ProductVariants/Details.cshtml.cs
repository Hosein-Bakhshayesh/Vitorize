using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.ProductVariants
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminProductModel Product { get; set; } = new();
        public AdminProductVariantModel Variant { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(Guid productId, Guid id)
        {
            Product = (await _apiClient.GetAsync<AdminProductModel>("admin/products/" + productId)).Data ?? new AdminProductModel{Id=productId};
            var result = await _apiClient.GetAsync<AdminProductVariantModel>("admin/product-variants/" + id);
            if (!result.IsSuccess || result.Data == null) { TempData["ErrorMessage"] = result.Message; return RedirectToPage("Index", new { productId }); }
            Variant = result.Data; Variant.CurrencyType = Product.CurrencyType;
            return Page();
        }
    }
}
