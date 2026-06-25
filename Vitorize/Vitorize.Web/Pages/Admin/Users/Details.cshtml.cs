using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Roles;
using Vitorize.Web.Models.Admin.Users;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Users
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminUserDetailModel UserDetail { get; set; } = new();
        public List<AdminRoleModel> AvailableRoles { get; set; } = new();
        [BindProperty] public UpdateUserRoleModel RoleRequest { get; set; } = new();
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var ok = await LoadAsync(id);
            return ok ? Page() : RedirectToPage("Index");
        }
        public async Task<IActionResult> OnPostAddRoleAsync(Guid id)
        {
            if (string.IsNullOrWhiteSpace(RoleRequest.RoleName))
            {
                TempData["ErrorMessage"] = "یک نقش را انتخاب کن.";
                return RedirectToPage(new { id });
            }
            var r = await _apiClient.PostAsync<object>($"admin/users/{id}/roles/add", RoleRequest);
            TempData[r.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = r.IsSuccess ? "نقش با موفقیت اضافه شد." : r.Message;
            return RedirectToPage(new { id });
        }
        public async Task<IActionResult> OnPostRemoveRoleAsync(Guid id, string roleName)
        {
            var r = await _apiClient.PostAsync<object>($"admin/users/{id}/roles/remove", new UpdateUserRoleModel { RoleName = roleName });
            TempData[r.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = r.IsSuccess ? "نقش با موفقیت حذف شد." : r.Message;
            return RedirectToPage(new { id });
        }
        private async Task<bool> LoadAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<AdminUserDetailModel>("admin/users/" + id);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(result.Message) ? "کاربر پیدا نشد." : result.Message;
                return false;
            }
            UserDetail = result.Data;
            var roles = await _apiClient.GetAsync<List<AdminRoleModel>>("admin/roles");
            AvailableRoles = roles.IsSuccess && roles.Data != null ? roles.Data : new();
            return true;
        }
    }
}
