using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        [BindProperty(SupportsGet = true)] public Guid? ProductId { get; set; }
        public List<AdminProductModel> Products { get; set; } = new();
        public AdminProductModel? Product { get; set; }
        public List<AdminProductVariantModel> Variants { get; set; } = new();
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task OnGetAsync() => await LoadAsync();
        public async Task<IActionResult> OnPostDeleteAsync(Guid productId, Guid id)
        {
            var result = await _apiClient.DeleteAsync("admin/product-variants/" + id);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "واریانت حذف شد." : result.Message;
            return RedirectToPage(new { productId });
        }
        private async Task LoadAsync()
        {
            var productsResult = await _apiClient.GetAsync<List<AdminProductModel>>("admin/products");
            Products = productsResult.IsSuccess && productsResult.Data != null ? productsResult.Data.OrderBy(x => x.Title).ToList() : new();
            if (ProductId.HasValue && ProductId.Value != Guid.Empty)
            {
                Product = Products.FirstOrDefault(x => x.Id == ProductId.Value);
                var result = await _apiClient.GetAsync<List<AdminProductVariantModel>>($"admin/products/{ProductId.Value}/variants");
                Variants = result.IsSuccess && result.Data != null ? result.Data.OrderByDescending(x => x.IsDefault).ThenBy(x => x.SortOrder).ToList() : new();
                foreach (var v in Variants) { v.ProductTitle = Product?.Title ?? v.ProductTitle; v.CurrencyType = Product?.CurrencyType ?? v.CurrencyType; }
            }
        }
    }
}
