using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages.Cart
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.CustomerScheme)]
    public class IndexModel : PageModel
    {
        private readonly IStorefrontApiService _storefront;

        public IndexModel(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        public StoreCartModel Cart { get; set; } = new();

        public List<StoreProductCardModel> SuggestedProducts { get; set; } = new();

        [TempData]
        public string? Message { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostUpdateAsync(Guid cartItemId, int quantity)
        {
            if (quantity <= 0)
            {
                await _storefront.RemoveCartItemAsync(cartItemId);
                Message = "محصول از سبد خرید حذف شد.";
                return RedirectToPage();
            }

            await _storefront.UpdateCartItemAsync(cartItemId, quantity);
            Message = "سبد خرید بروزرسانی شد.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(Guid cartItemId)
        {
            await _storefront.RemoveCartItemAsync(cartItemId);
            Message = "محصول از سبد خرید حذف شد.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearAsync()
        {
            await _storefront.ClearCartAsync();
            Message = "سبد خرید خالی شد.";

            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            Cart = await _storefront.GetCartAsync() ?? new StoreCartModel();
            SuggestedProducts = await _storefront.GetFeaturedProductsAsync(4);
        }
    }
}