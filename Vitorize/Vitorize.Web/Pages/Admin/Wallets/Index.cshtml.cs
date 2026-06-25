using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Wallet;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Wallets
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;

        [BindProperty(SupportsGet = true)] public Guid? UserId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        public List<WalletModel> Wallets { get; set; } = new();
        public WalletModel? Wallet { get; set; }
        public List<WalletTransactionModel> Transactions { get; set; } = new();
        [BindProperty] public WalletChargeRequestModel ChargeRequest { get; set; } = new();
        [BindProperty] public WalletWithdrawRequestModel WithdrawRequest { get; set; } = new();
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadWalletsAsync();
            if (UserId.HasValue) await LoadDetailsAsync(UserId.Value);
        }

        public async Task<IActionResult> OnPostChargeAsync(Guid userId)
        {
            ChargeRequest.UserId = userId;
            var r = await _apiClient.PostAsync<WalletModel>("admin/wallets/charge", ChargeRequest);
            TempData[r.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = r.IsSuccess ? "کیف پول با موفقیت شارژ شد." : r.Message;
            return RedirectToPage(new { userId, Search });
        }

        public async Task<IActionResult> OnPostWithdrawAsync(Guid userId)
        {
            WithdrawRequest.UserId = userId;
            var r = await _apiClient.PostAsync<WalletModel>("admin/wallets/withdraw", WithdrawRequest);
            TempData[r.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = r.IsSuccess ? "برداشت با موفقیت ثبت شد." : r.Message;
            return RedirectToPage(new { userId, Search });
        }

        private async Task LoadWalletsAsync()
        {
            var url = "admin/wallets?PageSize=200";
            if (!string.IsNullOrWhiteSpace(Search)) url += "&Search=" + Uri.EscapeDataString(Search);
            var r = await _apiClient.GetAsync<List<WalletModel>>(url);
            Wallets = r.IsSuccess && r.Data != null ? r.Data : new();
            if (!r.IsSuccess) ErrorMessage = r.Message;
        }

        private async Task LoadDetailsAsync(Guid userId)
        {
            var w = await _apiClient.GetAsync<WalletModel>("admin/wallets/" + userId);
            if (w.IsSuccess) Wallet = w.Data; else ErrorMessage = w.Message;
            var t = await _apiClient.GetAsync<List<WalletTransactionModel>>($"admin/wallets/{userId}/transactions");
            if (t.IsSuccess && t.Data != null) Transactions = t.Data.OrderByDescending(x => x.CreatedAt).ToList();
        }
    }
}
