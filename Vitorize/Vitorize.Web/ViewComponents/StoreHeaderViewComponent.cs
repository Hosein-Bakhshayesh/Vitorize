using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.ViewComponents
{
    public class StoreHeaderViewComponent : ViewComponent
    {
        private readonly IStorefrontApiService _storefront;

        public StoreHeaderViewComponent(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var authResult = await HttpContext.AuthenticateAsync(
                VitorizeAuthSchemes.CustomerScheme);

            var model = new StoreHeaderModel
            {
                IsAuthenticated = authResult.Succeeded &&
                                  authResult.Principal?.Identity?.IsAuthenticated == true
            };

            if (model.IsAuthenticated)
            {
                var user = await _storefront.GetCurrentUserAsync();
                var cart = await _storefront.GetCartAsync();

                model.FullName = user?.FullName;
                model.Mobile = user?.Mobile;
                model.CartCount = cart?.TotalQuantity ?? 0;
            }

            return View(model);
        }
    }
}