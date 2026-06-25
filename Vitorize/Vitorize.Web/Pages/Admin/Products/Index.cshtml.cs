using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Helpers;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<AdminProductModel> Products {
            get;
            set;
        }
        = new();
        [BindProperty(SupportsGet = true)] public string? Search {
            get;
            set;
        }
        [BindProperty(SupportsGet = true)] public byte? ProductType {
            get;
            set;
        }
        [BindProperty(SupportsGet = true)] public bool? IsActive {
            get;
            set;
        }
        [TempData] public string? SuccessMessage {
            get;
            set;
        }
        [TempData] public string? ErrorMessage {
            get;
            set;
        }
        public int TotalCount => Products.Count;
        public int ActiveCount => Products.Count(x => x.IsActive);
        public int FeaturedCount => Products.Count(x => x.IsFeatured);
        public int GiftCodeStock => Products.Sum(x => x.AvailableStock);
        public async Task OnGetAsync() => await LoadAsync();
        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync("admin/products/" + id);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "محصول با موفقیت حذف شد." : result.Message;
            return RedirectToPage();
        }
        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminProductModel>>("admin/products");
            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                Products = new();
                return;
            }
            Products = result.Data;
            if (!string.IsNullOrWhiteSpace(Search))
            Products = Products.Where(x => x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) || x.Slug.Contains(Search, StringComparison.OrdinalIgnoreCase) || (x.CategoryTitle?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false) || (x.BrandTitle?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            if (ProductType.HasValue)
            Products = Products.Where(x => x.ProductType == ProductType.Value).ToList();
            if (IsActive.HasValue)
            Products = Products.Where(x => x.IsActive == IsActive.Value).ToList();
            Products = Products.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ToList();
        }
        public string Image(string? path) => AdminUiHelper.ImageUrl(path);
    }
}
