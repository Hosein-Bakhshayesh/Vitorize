using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Shared.Enums;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Products
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme)]
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

        public string FormatMoney(decimal amount)
        {
            return amount.ToString("N0") + " تومان";
        }

        public string GetProductType(byte type)
        {
            return ((ProductType)type) switch
            {
                ProductType.GiftCard => "گیفت کارت",
                ProductType.GameAccount => "اکانت بازی",
                ProductType.GameService => "سرویس بازی",
                ProductType.Subscription => "اشتراک",
                ProductType.Other => "سایر",
                _ => "نامشخص"
            };
        }

        public string GetDeliveryType(byte type)
        {
            return ((DeliveryType)type) switch
            {
                DeliveryType.Instant => "تحویل آنی",
                DeliveryType.Manual => "تحویل دستی",
                DeliveryType.SupportRequired => "نیازمند پشتیبانی",
                _ => "نامشخص"
            };
        }

        public string GetCurrencyType(byte type)
        {
            return ((CurrencyType)type) switch
            {
                CurrencyType.Rial => "ریال",
                CurrencyType.Toman => "تومان",
                CurrencyType.USD => "دلار",
                _ => "نامشخص"
            };
        }
    }
}