using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IStorefrontApiService _storefront;

        public IndexModel(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        public List<StoreProductCardModel> FeaturedProducts { get; set; } = new();

        public List<StoreProductCardModel> PopularProducts { get; set; } = new();

        public List<StoreProductCardModel> ServiceProducts { get; set; } = new();

        public List<StoreProductLookupModel> Categories { get; set; } = new();

        public async Task OnGetAsync()
        {
            Categories = await _storefront.GetCategoriesAsync();

            FeaturedProducts = await _storefront.GetFeaturedProductsAsync(6);

            PopularProducts = await _storefront.GetProductsAsync(new StoreProductFilterModel
            {
                Page = 1,
                PageSize = 6
            });

            ServiceProducts = await _storefront.GetProductsAsync(new StoreProductFilterModel
            {
                Page = 1,
                PageSize = 5
            });
        }
    }
}