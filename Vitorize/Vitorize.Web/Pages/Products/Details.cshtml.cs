using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages.Products
{
    public class DetailsModel : PageModel
    {
        private readonly IStorefrontApiService _storefront;

        public DetailsModel(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        [BindProperty(SupportsGet = true)]
        public string Slug { get; set; } = string.Empty;

        [BindProperty]
        public Guid? ProductVariantId { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        public StoreProductDetailsModel? Product { get; set; }

        public StoreProductVariantModel? SelectedVariant =>
            Product?.Variants.FirstOrDefault(x => x.Id == ProductVariantId)
            ?? Product?.Variants.FirstOrDefault(x => x.IsDefault)
            ?? Product?.Variants.FirstOrDefault();

        public decimal CurrentPrice =>
            SelectedVariant?.FinalPrice ?? Product?.FinalPrice ?? 0;

        public int CurrentStock =>
            SelectedVariant?.AvailableStock ?? Product?.AvailableStock ?? 0;

        public async Task<IActionResult> OnGetAsync()
        {
            Product = await _storefront.GetProductBySlugAsync(Slug);

            if (Product == null)
                return NotFound();

            ProductVariantId =
                Product.Variants.FirstOrDefault(x => x.IsDefault)?.Id
                ?? Product.Variants.FirstOrDefault()?.Id;

            return Page();
        }

        public async Task<IActionResult> OnPostAddToCartAsync()
        {
            Product = await _storefront.GetProductBySlugAsync(Slug);

            if (Product == null)
                return NotFound();

            if (Quantity < Product.MinOrderQuantity)
                Quantity = Product.MinOrderQuantity;

            if (Product.MaxOrderQuantity.HasValue && Quantity > Product.MaxOrderQuantity.Value)
                Quantity = Product.MaxOrderQuantity.Value;

            await _storefront.AddToCartAsync(new StoreAddToCartRequestModel
            {
                ProductId = Product.Id,
                ProductVariantId = ProductVariantId,
                Quantity = Quantity
            });

            return RedirectToPage("/Cart/Index");
        }
    }
}