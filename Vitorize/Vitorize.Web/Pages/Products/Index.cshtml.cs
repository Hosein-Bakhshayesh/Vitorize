using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly IStorefrontApiService _storefront;

        public IndexModel(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? CategoryId { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? BrandId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? IsFeatured { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool OnlyAvailable { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 16;

        public List<StoreProductCardModel> Products { get; set; } = new();
        public List<StoreProductLookupModel> Categories { get; set; } = new();
        public List<StoreProductLookupModel> Brands { get; set; } = new();

        public async Task OnGetAsync()
        {
            if (PageNumber < 1)
                PageNumber = 1;

            Categories = await _storefront.GetCategoriesAsync();
            Brands = await _storefront.GetBrandsAsync();

            Products = await _storefront.GetProductsAsync(new StoreProductFilterModel
            {
                Search = Search,
                CategoryId = CategoryId,
                BrandId = BrandId,
                IsFeatured = IsFeatured,
                Page = PageNumber,
                PageSize = PageSize
            });

            if (OnlyAvailable)
            {
                Products = Products
                    .Where(x => x.AvailableStock > 0)
                    .ToList();
            }

            Products = Sort switch
            {
                "price-asc" => Products.OrderBy(x => x.FinalPrice).ToList(),
                "price-desc" => Products.OrderByDescending(x => x.FinalPrice).ToList(),
                "stock" => Products.OrderByDescending(x => x.AvailableStock).ToList(),
                "featured" => Products.OrderByDescending(x => x.IsFeatured).ToList(),
                _ => Products
            };
        }

        public string PageTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Search))
                    return $"نتیجه جستجو برای «{Search}»";

                if (CategoryId.HasValue)
                    return Categories.FirstOrDefault(x => x.Id == CategoryId)?.Title ?? "محصولات";

                if (BrandId.HasValue)
                    return Brands.FirstOrDefault(x => x.Id == BrandId)?.Title ?? "محصولات";

                if (IsFeatured == true)
                    return "پیشنهادهای ویژه";

                return "همه محصولات";
            }
        }
    }
}