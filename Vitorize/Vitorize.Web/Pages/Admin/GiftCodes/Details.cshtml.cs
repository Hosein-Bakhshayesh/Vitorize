using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.GiftCodes;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.GiftCodes
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public DetailsModel(ApiClient apiClient) => _apiClient = apiClient;
        public GiftCodeBatchModel Batch { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _apiClient.GetAsync<List<GiftCodeBatchModel>>("admin/giftcodes/batches");
            var batch = result.Data?.FirstOrDefault(x => x.Id == id);
            if (!result.IsSuccess || batch == null) { TempData["ErrorMessage"] = result.Message; return RedirectToPage("Index"); }
            Batch = batch; return Page();
        }
    }
}
