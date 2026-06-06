using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.GiftCodes;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.GiftCodes
{
    [Authorize]
    public class ImportModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public ImportModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [BindProperty]
        public GiftCodeImportRequestModel ImportRequest { get; set; } = new();

        [BindProperty]
        public string CodesText { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public Guid? ProductId { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid? VariantId { get; set; }

        public List<AdminProductModel> Products { get; set; } = new();

        public List<AdminProductVariantModel> Variants { get; set; } = new();

        public AdminProductModel? SelectedProduct { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadProductsAsync();

            if (ProductId.HasValue)
            {
                ImportRequest.ProductId = ProductId.Value;
                await LoadVariantsAsync(ProductId.Value);

                SelectedProduct = Products.FirstOrDefault(x => x.Id == ProductId.Value);
            }

            if (VariantId.HasValue)
            {
                ImportRequest.ProductVariantId = VariantId.Value;
            }

            ImportRequest.BatchTitle = $"Batch - {DateTime.Now:yyyyMMdd-HHmm}";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadProductsAsync();

            if (ImportRequest.ProductId != Guid.Empty)
            {
                await LoadVariantsAsync(ImportRequest.ProductId);
                SelectedProduct = Products.FirstOrDefault(x => x.Id == ImportRequest.ProductId);
            }

            var codes = ParseCodes(CodesText);

            if (!codes.Any())
            {
                ModelState.AddModelError(
                    nameof(CodesText),
                    "حداقل یک کد وارد کن.");
            }

            if (!ModelState.IsValid)
                return Page();

            ImportRequest.Codes = codes;

            if (ImportRequest.ProductVariantId == Guid.Empty)
                ImportRequest.ProductVariantId = null;

            var result = await _apiClient.PostAsync<GiftCodeBatchModel>(
                "admin/giftcodes/import",
                ImportRequest);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            TempData["SuccessMessage"] =
                $"بچ «{result.Data.BatchTitle}» با {result.Data.TotalCodes} کد وارد شد.";

            return RedirectToPage("/Admin/GiftCodes/Index");
        }

        private async Task LoadProductsAsync()
        {
            var result = await _apiClient.GetAsync<List<AdminProductModel>>(
                "admin/products");

            if (result.IsSuccess && result.Data != null)
            {
                Products = result.Data
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Title)
                    .ToList();
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }

        private async Task LoadVariantsAsync(Guid productId)
        {
            var result = await _apiClient.GetAsync<List<AdminProductVariantModel>>(
                $"admin/products/{productId}/variants");

            if (result.IsSuccess && result.Data != null)
            {
                Variants = result.Data
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.IsDefault)
                    .ThenBy(x => x.SortOrder)
                    .ToList();
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }

        private static List<string> ParseCodes(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(new[] { "\r\n", "\n", "\r", ",", ";", "\t" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }
    }
}