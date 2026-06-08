using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages.Checkout
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

        [BindProperty]
        public StoreCheckoutRequestModel RequestModel { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadCartAsync();

            if (!Cart.Items.Any())
                return RedirectToPage("/Cart/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCartAsync();

            if (!Cart.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "سبد خرید شما خالی است.");
                return Page();
            }

            var checkout = await _storefront.CheckoutAsync(RequestModel);

            if (checkout == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "ثبت سفارش انجام نشد. لطفاً وضعیت ورود، موجودی محصولات و اتصال API را بررسی کنید.");

                return Page();
            }

            var payment = await _storefront.StartPaymentAsync(checkout.OrderId);

            if (payment == null)
            {
                return RedirectToPage("/Orders/Index", new
                {
                    OrderId = checkout.OrderId
                });
            }

            return RedirectToPage("/Payments/Mock", new
            {
                paymentId = payment.PaymentId,
                orderId = payment.OrderId,
                amount = payment.Amount
            });
        }

        private async Task LoadCartAsync()
        {
            Cart = await _storefront.GetCartAsync() ?? new StoreCartModel();
        }
    }
}