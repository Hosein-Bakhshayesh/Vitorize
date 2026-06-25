using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Settings;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Settings
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<SettingGroupModel> Groups { get; set; } = new();
        [BindProperty] public UpdateSettingModel Input { get; set; } = new();
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }
        public async Task OnGetAsync() => await LoadAsync();
        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.Key))
            {
                TempData["ErrorMessage"] = "تنظیم موردنظر معتبر نیست.";
                return RedirectToPage();
            }
            if (string.Equals(Input.ValueType, "bool", StringComparison.OrdinalIgnoreCase))
                Input.Value = string.Equals(Input.Value, "true", StringComparison.OrdinalIgnoreCase) || Input.Value == "on" ? "true" : "false";
            var result = await _apiClient.PostAsync<SettingModel>("admin/settings", Input);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "تنظیمات با موفقیت ذخیره شد." : result.Message;
            return RedirectToPage();
        }
        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<SettingGroupModel>>("admin/settings");
            Groups = result.IsSuccess && result.Data != null ? result.Data : new();
            if (!result.IsSuccess) ErrorMessage = result.Message;
        }
        public List<SettingModel> SettingsOf(SettingGroupModel group) => group.Settings.Any() ? group.Settings : group.Items;
        public string GroupTitle(string? groupName) => groupName switch
        {
            "General" => "عمومی",
            "Support" => "پشتیبانی",
            "Social" => "شبکه‌های اجتماعی",
            "Features" => "قابلیت‌ها",
            "SMS" => "پیامک",
            "Wallet" => "کیف پول",
            "Payment" => "پرداخت",
            _ => string.IsNullOrWhiteSpace(groupName) ? "سایر" : groupName
        };
    }
}
