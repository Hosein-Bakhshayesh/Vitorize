using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages.Payments
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.CustomerScheme)]
    public class MockModel : PageModel
    {
        private readonly IStorefrontApiService _storefront;

        public MockModel(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        [BindProperty(SupportsGet = true)]
        public Guid PaymentId { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid OrderId { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal Amount { get; set; }

        public IActionResult OnGet()
        {
            if (PaymentId == Guid.Empty || OrderId == Guid.Empty)
                return RedirectToPage("/Orders/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostPayAsync()
        {
            var result = await _storefront.VerifyMockPaymentAsync(PaymentId);

            if (result == null || !result.IsSuccess)
            {
                TempData["PaymentError"] = "پرداخت تستی تایید نشد.";
                return RedirectToPage("/Orders/Index", new { OrderId });
            }

            return RedirectToPage("/Orders/Index", new { OrderId });
        }

        public IActionResult OnPostCancel()
        {
            return RedirectToPage("/Orders/Index", new { OrderId });
        }
    }
}