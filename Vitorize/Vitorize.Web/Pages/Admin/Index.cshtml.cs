using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Dashboard;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public AdminDashboardModel Dashboard { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            var result = await _apiClient.GetAsync<AdminDashboardModel>(
                "admin/dashboard");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return;
            }

            Dashboard = result.Data;
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string GetOrderStatus(byte status)
        {
            return status switch
            {
                1 => "در انتظار",
                2 => "در حال پردازش",
                3 => "تکمیل شده",
                4 => "لغو شده",
                _ => "نامشخص"
            };
        }

        public string GetStatusCss(byte status)
        {
            return status switch
            {
                1 => "status-process",
                2 => "status-process",
                3 => "status-success",
                4 => "status-danger",
                _ => "status-process"
            };
        }
    }
}