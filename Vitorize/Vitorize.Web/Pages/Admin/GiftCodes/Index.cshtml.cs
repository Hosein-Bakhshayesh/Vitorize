using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.GiftCodes;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.GiftCodes
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public IndexModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public List<GiftCodeBatchModel> Batches { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public int TotalBatches => Batches.Count;

        public int TotalCodes => Batches.Sum(x => x.TotalCodes);

        public int AvailableCodes => Batches.Sum(x => x.AvailableCodes);

        public int SoldCodes => Batches.Sum(x => x.SoldCodes);

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var result = await _apiClient.DeleteAsync(
                $"admin/giftcodes/batches/{id}");

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "بچ کدها با موفقیت حذف شد."
                    : result.Message;

            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            ErrorMessage = TempData["ErrorMessage"]?.ToString();

            var result = await _apiClient.GetAsync<List<GiftCodeBatchModel>>(
                "admin/giftcodes/batches");

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return;
            }

            Batches = result.Data
                .OrderByDescending(x => x.ImportedAt)
                .ToList();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                Batches = Batches
                    .Where(x =>
                        x.BatchTitle.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                        (x.SourceName?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }
        }

        public string FormatDate(DateTime date)
        {
            return date.ToString("yyyy/MM/dd HH:mm");
        }
    }
}