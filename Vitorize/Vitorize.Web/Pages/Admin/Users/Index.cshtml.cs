using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Common;
using Vitorize.Web.Models.Admin.Users;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Users
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<AdminUserModel> Users { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task OnGetAsync()
        {
            var url = "admin/users?page=1&pageSize=100" + (string.IsNullOrWhiteSpace(Search) ? "" : "&search=" + Uri.EscapeDataString(Search));
            var result = await _apiClient.GetAsync<AdminPagedResultModel<AdminUserModel>>(url);
            if (result.IsSuccess && result.Data != null) Users = result.Data.Rows; else ErrorMessage = result.Message;
        }
        public async Task<IActionResult> OnPostStatusAsync(Guid id, string action)
        {
            var endpoint = action switch { "activate" => "activate", "suspend" => "suspend", "block" => "block", _ => "activate" };
            var result = await _apiClient.PostAsync<object>($"admin/users/{id}/{endpoint}", new { });
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "وضعیت کاربر به‌روزرسانی شد." : result.Message;
            return RedirectToPage();
        }
    }
}
