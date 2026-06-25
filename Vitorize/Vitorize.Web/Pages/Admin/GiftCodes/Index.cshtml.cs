using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.GiftCodes;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.GiftCodes
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public IndexModel(ApiClient apiClient) => _apiClient = apiClient;
        public List<GiftCodeBatchModel> Batches {
            get;
            set;
        }
        = new();
        [BindProperty(SupportsGet = true)] public string? Search {
            get;
            set;
        }
        [TempData] public string? SuccessMessage {
            get;
            set;
        }
        [TempData] public string? ErrorMessage {
            get;
            set;
        }
        public int TotalCodes => Batches.Sum(x => x.TotalCodes);
        public int AvailableCodes => Batches.Sum(x => x.AvailableCodes);
        public int SoldCodes => Batches.Sum(x => x.SoldCodes);
        public async Task OnGetAsync() => await LoadAsync();
        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync("admin/giftcodes/batches/" + id);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "بچ حذف شد." : result.Message;
            return RedirectToPage();
        }
        private async Task LoadAsync()
        {
            var result = await _apiClient.GetAsync<List<GiftCodeBatchModel>>("admin/giftcodes/batches");
            Batches = result.IsSuccess && result.Data != null ? result.Data : new();
            if (!string.IsNullOrWhiteSpace(Search)) Batches = Batches.Where(x => x.BatchTitle.Contains(Search, StringComparison.OrdinalIgnoreCase) || (x.SourceName?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            Batches = Batches.OrderByDescending(x => x.ImportedAt).ToList();
        }
    }
}
