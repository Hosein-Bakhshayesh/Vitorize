using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Orders;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Orders
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<AdminOrderModel> Orders { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task OnGetAsync()
        {
            var url = string.IsNullOrWhiteSpace(Search) ? "admin/orders" : "admin/orders/search?search=" + Uri.EscapeDataString(Search);
            var result = await _apiClient.GetAsync<List<AdminOrderModel>>(url);
            if (result.IsSuccess && result.Data != null) Orders = result.Data.OrderByDescending(x => x.CreatedAt).ToList(); else ErrorMessage = result.Message;
        }
    }
}
