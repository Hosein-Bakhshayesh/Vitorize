using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Dashboard;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminDashboardModel Dashboard { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public async Task OnGetAsync()
        {
            var result = await _apiClient.GetAsync<AdminDashboardModel>("admin/dashboard");
            if (result.IsSuccess && result.Data != null) Dashboard = result.Data; else ErrorMessage = result.Message;
        }
        public decimal MaxSales => Dashboard.SalesLast7Days.Any() ? Math.Max(1, Dashboard.SalesLast7Days.Max(x => x.Value)) : 1;
        public decimal MaxOrders => Dashboard.OrdersLast7Days.Any() ? Math.Max(1, Dashboard.OrdersLast7Days.Max(x => x.Value)) : 1;
    }
}
