using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Models.Admin.ProductVariants;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
namespace Vitorize.Web.Pages.Admin.ProductVariants
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;
        public EditModel(ApiClient apiClient) => _apiClient = apiClient;
        public AdminProductModel Product {
            get;
            set;
        }
        = new();
        [BindProperty] public UpdateProductVariantRequestModel Input {
            get;
            set;
        }
        = new();
        public string? ErrorMessage {
            get;
            set;
        }
        public async Task<IActionResult> OnGetAsync(Guid productId, Guid id)
        {
            var productResult = await _apiClient.GetAsync<AdminProductModel>("admin/products/" + productId);
            if (!productResult.IsSuccess || productResult.Data == null) {
                TempData["ErrorMessage"] = productResult.Message;
                return RedirectToPage("/Admin/Products/Index");
            }
            Product = productResult.Data;
            var result = await _apiClient.GetAsync<AdminProductVariantModel>("admin/product-variants/" + id);
            if (!result.IsSuccess || result.Data == null) {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToPage("Index", new {
                    productId }
                    );
                }
                Input = new UpdateProductVariantRequestModel {
                    Title = result.Data.Title, Sku = result.Data.Sku, Price = result.Data.Price, DiscountPrice = result.Data.DiscountPrice, Value = result.Data.Value, StockMode = result.Data.StockMode, IsDefault = result.Data.IsDefault, IsActive = result.Data.IsActive, SortOrder = result.Data.SortOrder }
                    ;
                    return Page();
                }
                public async Task<IActionResult> OnPostAsync(Guid productId, Guid id)
                {
                    var productResult = await _apiClient.GetAsync<AdminProductModel>("admin/products/" + productId);
                    Product = productResult.Data ?? new AdminProductModel {
                        Id = productId }
                        ;
                        if (Input.DiscountPrice.HasValue && Input.DiscountPrice.Value <= 0) Input.DiscountPrice = null;
                        if (Input.DiscountPrice.HasValue && Input.DiscountPrice.Value >= Input.Price) ModelState.AddModelError("Input.DiscountPrice", "قیمت تخفیفی باید کمتر از قیمت اصلی باشد.");
                        if (!ModelState.IsValid) return Page();
                        var result = await _apiClient.PutAsync<AdminProductVariantModel>("admin/product-variants/" + id, Input);
                        if (!result.IsSuccess || result.Data == null) {
                            ErrorMessage = result.Message;
                            return Page();
                        }
                        TempData["SuccessMessage"] = "واریانت با موفقیت ذخیره شد.";
                        return RedirectToPage("Index", new {
                            productId }
                            );
                        }
                    }
                }
