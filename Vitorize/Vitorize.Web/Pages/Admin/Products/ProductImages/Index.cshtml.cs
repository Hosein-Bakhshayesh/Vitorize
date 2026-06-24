using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Constants;
using Vitorize.Web.Models.Admin.ProductImages;
using Vitorize.Web.Models.Admin.Products;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;

namespace Vitorize.Web.Pages.Admin.ProductImages
{
    [Authorize(AuthenticationSchemes = VitorizeAuthSchemes.AdminScheme, Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly IFileStorageService _fileStorageService;

        private const long MaxImageSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly string[] AllowedImageContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

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
            if (productId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "شناسه محصول معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;

            var loaded = await LoadAsync(productId);

            if (!loaded)
                return RedirectToPage("/Admin/Products/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "شناسه محصول معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            ProductId = productId;

            var loaded = await LoadAsync(productId);

            if (!loaded)
                return RedirectToPage("/Admin/Products/Index");

            var validationError = ValidateImageFile(ImageFile);

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                TempData["ErrorMessage"] = validationError;
                return RedirectToPage(new { productId });
            }

            var savedImagePath = string.Empty;

            try
            {
                savedImagePath = await _fileStorageService.SaveAsync(
                    ImageFile,
                    StorageFolders.Products,
                    HttpContext.RequestAborted) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(savedImagePath))
                {
                    TempData["ErrorMessage"] = "فایل تصویر انتخاب نشده است.";
                    return RedirectToPage(new { productId });
                }

                var request = new CreateProductImageRequestModel
                {
                    ImagePath = savedImagePath,
                    AltText = NormalizeText(AltText) ?? Product?.Title,
                    SortOrder = ResolveSortOrder(),
                    SetAsThumbnail = SetAsThumbnail || !Images.Any()
                };

                var result = await _apiClient.PostAsync<AdminProductImageModel>(
                    $"admin/products/{productId}/images",
                    request);

                if (!result.IsSuccess || result.Data == null)
                {
                    await _fileStorageService.DeleteAsync(
                        savedImagePath,
                        HttpContext.RequestAborted);

                    TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(result.Message)
                        ? "ثبت مسیر تصویر در دیتابیس با خطا مواجه شد."
                        : result.Message;

                    return RedirectToPage(new { productId });
                }

                TempData["SuccessMessage"] =
                    "تصویر با موفقیت آپلود شد و مسیر آن در دیتابیس ثبت شد.";

                return RedirectToPage(new { productId });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(savedImagePath))
                {
                    await _fileStorageService.DeleteAsync(
                        savedImagePath,
                        HttpContext.RequestAborted);
                }

                TempData["ErrorMessage"] = ex.Message;
                return RedirectToPage(new { productId });
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            Guid productId,
            Guid imageId,
            string? altText,
            int sortOrder)
        {
            if (productId == Guid.Empty || imageId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "اطلاعات تصویر معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            var currentImage = await FindImageAsync(productId, imageId);

            if (currentImage == null)
            {
                TempData["ErrorMessage"] = "تصویر مورد نظر پیدا نشد.";
                return RedirectToPage(new { productId });
            }

            var request = new UpdateProductImageRequestModel
            {
                ImagePath = currentImage.ImagePath,
                AltText = NormalizeText(altText),
                SortOrder = sortOrder < 0 ? 0 : sortOrder,
                IsThumbnail = currentImage.IsThumbnail
            };

            var result = await _apiClient.PutAsync<AdminProductImageModel>(
                $"admin/product-images/{imageId}",
                request);

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "اطلاعات تصویر با موفقیت ذخیره شد."
                    : string.IsNullOrWhiteSpace(result.Message)
                        ? "ذخیره اطلاعات تصویر با خطا مواجه شد."
                        : result.Message;

            return RedirectToPage(new { productId });
        }

        public async Task<IActionResult> OnPostSetThumbnailAsync(
            Guid productId,
            Guid imageId)
        {
            if (productId == Guid.Empty || imageId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "اطلاعات تصویر معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            var result = await _apiClient.PostAsync<object>(
                $"admin/product-images/{imageId}/set-thumbnail",
                new { });

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "تصویر اصلی محصول با موفقیت تغییر کرد."
                    : string.IsNullOrWhiteSpace(result.Message)
                        ? "تغییر تصویر اصلی محصول با خطا مواجه شد."
                        : result.Message;

            return RedirectToPage(new { productId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(
            Guid productId,
            Guid imageId)
        {
            if (productId == Guid.Empty || imageId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "اطلاعات تصویر معتبر نیست.";
                return RedirectToPage("/Admin/Products/Index");
            }

            var currentImage = await FindImageAsync(productId, imageId);

            if (currentImage == null)
            {
                TempData["ErrorMessage"] = "تصویر مورد نظر پیدا نشد.";
                return RedirectToPage(new { productId });
            }

            var result = await _apiClient.DeleteAsync(
                $"admin/product-images/{imageId}");

            if (result.IsSuccess)
            {
                await _fileStorageService.DeleteAsync(
                    currentImage.ImagePath,
                    HttpContext.RequestAborted);
            }

            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.IsSuccess
                    ? "تصویر با موفقیت از دیتابیس و سرور حذف شد."
                    : string.IsNullOrWhiteSpace(result.Message)
                        ? "حذف تصویر با خطا مواجه شد."
                        : result.Message;

            return RedirectToPage(new { productId });
        }

        private async Task<bool> LoadAsync(Guid productId)
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

            var productResult = await _apiClient.GetAsync<AdminProductModel>(
                $"admin/products/{productId}");

            if (!productResult.IsSuccess || productResult.Data == null)
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(productResult.Message)
                    ? "محصول مورد نظر پیدا نشد."
                    : productResult.Message;

                return false;
            }

            Product = productResult.Data;

            var imagesResult = await _apiClient.GetAsync<List<AdminProductImageModel>>(
                $"admin/products/{productId}/images");

            if (imagesResult.IsSuccess && imagesResult.Data != null)
            {
                Images = imagesResult.Data
                    .OrderByDescending(x => x.IsThumbnail)
                    .ThenBy(x => x.SortOrder)
                    .ThenByDescending(x => x.CreatedAt)
                    .ToList();
            }
            else
            {
                Images = new List<AdminProductImageModel>();

                if (!string.IsNullOrWhiteSpace(imagesResult.Message))
                    ErrorMessage = imagesResult.Message;
            }

            return true;
        }

        private async Task<AdminProductImageModel?> FindImageAsync(
            Guid productId,
            Guid imageId)
        {
            var result = await _apiClient.GetAsync<List<AdminProductImageModel>>(
                $"admin/products/{productId}/images");

            if (!result.IsSuccess || result.Data == null)
                return null;

            return result.Data.FirstOrDefault(x => x.Id == imageId);
        }

        private int ResolveSortOrder()
        {
            if (SortOrder > 0)
                return SortOrder;

            if (!Images.Any())
                return 10;

            return Images.Max(x => x.SortOrder) + 10;
        }

        private static string? ValidateImageFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return "لطفاً فایل تصویر را انتخاب کنید.";

            if (file.Length > MaxImageSize)
                return "حجم تصویر نباید بیشتر از ۵ مگابایت باشد.";

            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
                return "فرمت تصویر مجاز نیست. فقط jpg، jpeg، png و webp قابل قبول است.";

            if (!AllowedImageContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                return "نوع فایل انتخاب‌شده معتبر نیست.";

            return null;
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        public string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        public string GetProductInitial()
        {
            if (string.IsNullOrWhiteSpace(Product?.Title))
                return "V";

            return Product.Title.Trim()[0].ToString();
        }

        public string GetProductImagePath()
        {
            if (!string.IsNullOrWhiteSpace(Product?.ThumbnailImagePath))
                return GetImagePath(Product.ThumbnailImagePath);

            var thumbnail = Images.FirstOrDefault(x => x.IsThumbnail);

            if (thumbnail != null)
                return GetImagePath(thumbnail.ImagePath);

            return string.Empty;
        }

        public string GetImagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim();

            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/"))
            {
                return path;
            }

            return "/" + path.TrimStart('~', '/');
        }
    }
}