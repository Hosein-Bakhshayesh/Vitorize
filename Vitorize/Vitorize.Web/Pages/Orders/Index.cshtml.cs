using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Storefront;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web.Pages.Orders
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.CustomerScheme)]
    public class IndexModel : PageModel
    {
        private readonly IStorefrontApiService _storefront;

        public IndexModel(IStorefrontApiService storefront)
        {
            _storefront = storefront;
        }

        [BindProperty(SupportsGet = true)]
        public Guid? OrderId { get; set; }

        public List<StoreOrderModel> Orders { get; set; } = new();

        public StoreOrderModel? SelectedOrder { get; set; }

        public int TotalOrders => Orders.Count;

        public int DeliveredItems =>
            SelectedOrder?.Items.Count(x => x.Deliveries.Any(d =>
                d.IsVisibleToCustomer &&
                !string.IsNullOrWhiteSpace(d.DeliveredContent))) ?? 0;

        public async Task OnGetAsync()
        {
            Orders = await _storefront.GetMyOrdersAsync();

            if (OrderId.HasValue)
            {
                SelectedOrder = await _storefront.GetMyOrderDetailsAsync(OrderId.Value);
            }
            else
            {
                var firstOrder = Orders
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                if (firstOrder != null)
                    SelectedOrder = await _storefront.GetMyOrderDetailsAsync(firstOrder.Id);
            }
        }
    }
}