using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public DetailsModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public AdminProductModel Product { get; set; } = new();

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            ErrorMessage = TempData["ErrorMessage"]?.ToString();

            var result = await _apiClient.GetAsync<AdminProductModel>($"admin/products/{id}");

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("/Admin/Products/Index");
            }

            Product = result.Data;
            return Page();
        }

        public string FormatMoney(decimal amount) => amount.ToString("N0") + " تومان";

        public string GetProductType(byte type) => type switch
        {
            1 => "گیفت کارت",
            2 => "اکانت بازی",
            3 => "سرویس دیجیتال",
            _ => "نامشخص"
        };

        public string GetDeliveryType(byte type) => type switch
        {
            1 => "تحویل آنی",
            2 => "تحویل دستی",
            3 => "تیکتی",
            _ => "نامشخص"
        };

        public string GetCurrencyType(byte type) => type switch
        {
            1 => "تومان",
            2 => "ریال",
            3 => "دلار",
            _ => "نامشخص"
        };
    }
}