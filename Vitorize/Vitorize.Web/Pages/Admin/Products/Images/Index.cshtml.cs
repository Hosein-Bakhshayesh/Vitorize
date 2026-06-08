using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Models.Admin.ProductImages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.Products.Images
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme)]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorageService;

        public IndexModel(
            ApiClient apiClient,
            IFileStorageService fileStorageService)
        {
            _apiClient = apiClient;
            _fileStorageService = fileStorageService;
        }

        public Guid ProductId { get; set; }

        public AdminProductModel? Product { get; set; }

        public List<AdminProductImageModel> Images { get; set; } = new();

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        [BindProperty]
        public string? AltText { get; set; }

        [BindProperty]
        public int SortOrder { get; set; }

        [BindProperty]
        public bool SetAsThumbnail { get; set; }

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid productId)
        {
            ProductId = productId;
            await LoadAsync(productId);
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(Guid productId)
        {
            ProductId = productId;

            try
            {
                var imagePath = await _fileStorageService.SaveAsync(
                    ImageFile,
                    StorageFolders.Products,
                    HttpContext.RequestAborted);

                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    TempData["ErrorMessage"] = "فایل تصویر انتخاب نشده است.";
                    return RedirectToPage(new { productId });
                }

                var request = new CreateProductImageRequestModel
                {
                    ImagePath = imagePath,
                    AltText = AltText,
                    SortOrder = SortOrder,
                    SetAsThumbnail = SetAsThumbnail
                };

                var result = await _apiClient.PostAsync<AdminProductImageModel>(
                    $"admin/products/{productId}/images",
                    request);

                if (!result.IsSuccess)
                {
                    await _fileStorageService.DeleteAsync(
                        imagePath,
                        HttpContext.RequestAborted);

                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToPage(new { productId });
                }

                TempData["SuccessMessage"] = "تصویر با موفقیت آپلود شد.";
                return RedirectToPage(new { productId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToPage(new { productId });
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            Guid productId,
            Guid imageId,
            string imagePath,
            string? altText,
            int sortOrder,
            bool isThumbnail)
        {
            var request = new UpdateProductImageRequestModel
            {
                ImagePath = imagePath,
                AltText = altText,
                SortOrder = sortOrder,
                IsThumbnail = isThumbnail
            };

            var result = await _apiClient.PutAsync<AdminProductImageModel>(
                $"admin/product-images/{imageId}",
                request);

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "تصویر با موفقیت ویرایش شد."
                    : result.Message;

            return RedirectToPage(new { productId });
        }

        public async Task<IActionResult> OnPostSetThumbnailAsync(
            Guid productId,
            Guid imageId)
        {
            var result = await _apiClient.PostAsync<object>(
                $"admin/product-images/{imageId}/set-thumbnail",
                new { });

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "تصویر اصلی محصول تغییر کرد."
                    : result.Message;

            return RedirectToPage(new { productId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(
            Guid productId,
            Guid imageId,
            string? imagePath)
        {
            var result = await _apiClient.DeleteAsync(
                $"admin/product-images/{imageId}");

            if (result.IsSuccess)
            {
                await _fileStorageService.DeleteAsync(
                    imagePath,
                    HttpContext.RequestAborted);
            }

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "تصویر با موفقیت حذف شد."
                    : result.Message;

            return RedirectToPage(new { productId });
        }

        private async Task LoadAsync(Guid productId)
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

            var productResult = await _apiClient.GetAsync<AdminProductModel>(
                $"admin/products/{productId}");

            if (productResult.IsSuccess && productResult.Data != null)
            {
                Product = productResult.Data;
            }
            else
            {
                ErrorMessage = productResult.Message;
            }

            var imagesResult = await _apiClient.GetAsync<List<AdminProductImageModel>>(
                $"admin/products/{productId}/images");

            if (imagesResult.IsSuccess && imagesResult.Data != null)
            {
                Images = imagesResult.Data
                    .OrderByDescending(x => x.IsThumbnail)
                    .ThenBy(x => x.SortOrder)
                    .ToList();
            }
            else
            {
                ErrorMessage = imagesResult.Message;
            }
        }
    }
}