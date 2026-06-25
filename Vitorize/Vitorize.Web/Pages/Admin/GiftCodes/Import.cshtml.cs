using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.GiftCodes;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.GiftCodes
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class ImportModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public ImportModel(ApiClient apiClient) => _apiClient = apiClient;
        [BindProperty] public GiftCodeImportRequestModel ImportRequest {
            get;
            set;
        }
        = new();
        [BindProperty] public string CodesText {
            get;
            set;
        }
        = string.Empty;
        [BindProperty(SupportsGet = true)] public Guid? ProductId {
            get;
            set;
        }
        public List<AdminProductModel> Products {
            get;
            set;
        }
        = new();
        public List<AdminProductVariantModel> Variants {
            get;
            set;
        }
        = new();
        public string? ErrorMessage {
            get;
            set;
        }
        public async Task OnGetAsync() {
            await LoadProductsAsync();
            ImportRequest.BatchTitle = $"Batch-{DateTime.Now:yyyyMMdd-HHmm}";
            if(ProductId.HasValue){
                ImportRequest.ProductId=ProductId.Value;
                await LoadVariantsAsync(ProductId.Value);
            }
        }
        public async Task<IActionResult> OnPostAsync()
        {
            await LoadProductsAsync();
            if(ImportRequest.ProductId != Guid.Empty) await LoadVariantsAsync(ImportRequest.ProductId);
            var codes = ParseCodes(CodesText);
            if(!codes.Any()) ModelState.AddModelError(nameof(CodesText), "حداقل یک کد وارد کن.");
            if(!ModelState.IsValid) return Page();
            ImportRequest.Codes = codes;
            if(ImportRequest.ProductVariantId == Guid.Empty) ImportRequest.ProductVariantId = null;
            var result = await _apiClient.PostAsync<GiftCodeBatchModel>("admin/giftcodes/import", ImportRequest);
            if(!result.IsSuccess || result.Data == null){
                ErrorMessage = result.Message;
                return Page();
            }
            TempData["SuccessMessage"] = "کدها با موفقیت وارد شدند.";
            return RedirectToPage("Index");
        }
        private async Task LoadProductsAsync(){
            var r=await _apiClient.GetAsync<List<AdminProductModel>>("admin/products");
            Products = r.IsSuccess && r.Data!=null ? r.Data.Where(x=>x.IsActive).OrderBy(x=>x.Title).ToList() : new();
        }
        private async Task LoadVariantsAsync(Guid productId){
            var r=await _apiClient.GetAsync<List<AdminProductVariantModel>>($"admin/products/{productId}/variants");
            Variants = r.IsSuccess && r.Data!=null ? r.Data.Where(x=>x.IsActive).OrderByDescending(x=>x.IsDefault).ThenBy(x=>x.SortOrder).ToList() : new();
        }
        private static List<string> ParseCodes(string? value) => string.IsNullOrWhiteSpace(value) ? new() : value.Split(new[] {
            "\r\n", "\n", "\r", ",", ";", "\t" }
            , StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
