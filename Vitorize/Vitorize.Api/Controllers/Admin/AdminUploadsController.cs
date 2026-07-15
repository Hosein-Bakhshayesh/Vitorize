using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    /// <summary>
    /// آپلود تصاویر پنل مدیریت. هر نوع تصویر در پوشه‌ی مخصوص خود ذخیره می‌شود
    /// و مسیر نسبی /uploads/... برگردانده می‌شود که روی میزبان API سرو می‌شود.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/uploads")]
    public class AdminUploadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };
        private static readonly string[] SettingsAllowedExtensions = [.. AllowedExtensions, ".ico"];
        private static readonly string[] SettingsAllowedContentTypes = [.. AllowedContentTypes, "image/x-icon", "image/vnd.microsoft.icon"];

        private const long MaxFileSize = 2 * 1024 * 1024;

        public AdminUploadsController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("product-image")]
        [RequestSizeLimit(MaxFileSize)]
        public Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadProductImage(IFormFile file)
            => UploadAsync(file, "products", "تصویر محصول با موفقیت آپلود شد.");

        [HttpPost("category-image")]
        [RequestSizeLimit(MaxFileSize)]
        public Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadCategoryImage(IFormFile file)
            => UploadAsync(file, "categories", "تصویر دسته‌بندی با موفقیت آپلود شد.");

        [HttpPost("brand-image")]
        [RequestSizeLimit(MaxFileSize)]
        public Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadBrandImage(IFormFile file)
            => UploadAsync(file, "brands", "لوگوی برند با موفقیت آپلود شد.");

        [HttpPost("banner-image")]
        [RequestSizeLimit(MaxFileSize)]
        public Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadBannerImage(IFormFile file)
            => UploadAsync(file, "banners", "تصویر بنر با موفقیت آپلود شد.");

        [HttpPost("settings-image")]
        [RequestSizeLimit(MaxFileSize)]
        public Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadSettingsImage(IFormFile file)
            => UploadAsync(file, "settings", "تصویر با موفقیت آپلود شد.", SettingsAllowedExtensions, SettingsAllowedContentTypes);

        private async Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadAsync(
            IFormFile file,
            string folderName,
            string successMessage,
            string[]? extensions = null,
            string[]? contentTypes = null)
        {
            var result = await UploadHelper.SaveImageAsync(
                _environment, file, folderName, MaxFileSize, extensions ?? AllowedExtensions, contentTypes ?? AllowedContentTypes);

            return Ok(ApiResult<UploadFileResultDto>.Success(result, successMessage));
        }
    }

    /// <summary>
    /// منطق مشترک ذخیره‌ی امن تصویر: اعتبارسنجی پسوند، نوع محتوا و امضای باینری فایل.
    /// </summary>
    internal static class UploadHelper
    {
        public static async Task<UploadFileResultDto> SaveImageAsync(
            IWebHostEnvironment environment,
            IFormFile file,
            string folderName,
            long maxFileSize,
            string[] allowedExtensions,
            string[] allowedContentTypes)
        {
            if (file == null || file.Length == 0)
                throw new BusinessException("فایل تصویر ارسال نشده است.");

            if (file.Length > maxFileSize)
                throw new BusinessException($"حجم تصویر نباید بیشتر از {maxFileSize / (1024 * 1024)} مگابایت باشد.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                throw new BusinessException("فرمت تصویر مجاز نیست.");

            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new BusinessException("نوع فایل معتبر نیست.");

            // WebRootPath وقتی wwwroot هنگام شروع برنامه وجود نداشته باشد null است.
            var webRoot = environment.WebRootPath
                ?? Path.Combine(environment.ContentRootPath, "wwwroot");

            var uploadRoot = Path.Combine(webRoot, "uploads", folderName);
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // اعتبارسنجی امضای باینری؛ جلوی آپلود فایل غیرتصویری با پسوند جعلی را می‌گیرد.
            if (!await IsValidImageSignatureAsync(fullPath))
            {
                System.IO.File.Delete(fullPath);
                throw new BusinessException("محتوای فایل با یک تصویر معتبر مطابقت ندارد.");
            }

            return new UploadFileResultDto
            {
                FileName = fileName,
                FilePath = $"/uploads/{folderName}/{fileName}",
                ContentType = file.ContentType,
                Size = file.Length
            };
        }

        public static async Task<UploadFileResultDto> SavePrivateImageAsync(
            IWebHostEnvironment environment,
            IFormFile file,
            string ownerFolder,
            long maxFileSize,
            string[] allowedExtensions,
            string[] allowedContentTypes)
        {
            if (file == null || file.Length == 0 || file.Length > maxFileSize)
                throw new BusinessException("فایل معتبر نیست یا از حداکثر حجم مجاز بیشتر است.");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension) ||
                !allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new BusinessException("نوع یا پسوند فایل مجاز نیست.");
            if (ownerFolder.Any(c => !char.IsAsciiHexDigit(c)))
                throw new BusinessException("مسیر مالک فایل معتبر نیست.");

            var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "private", "verification-documents"));
            var ownerRoot = Path.GetFullPath(Path.Combine(root, ownerFolder));
            if (!ownerRoot.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("مسیر فایل معتبر نیست.");
            Directory.CreateDirectory(ownerRoot);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(ownerRoot, fileName);
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(stream);
            if (!await IsValidImageSignatureAsync(fullPath))
            {
                System.IO.File.Delete(fullPath);
                throw new BusinessException("محتوای فایل تصویر معتبر نیست.");
            }
            return new UploadFileResultDto
            {
                FileName = fileName,
                FilePath = $"kyc-private:{ownerFolder}/{fileName}",
                ContentType = file.ContentType,
                Size = file.Length
            };
        }

        private static async Task<bool> IsValidImageSignatureAsync(string path)
        {
            var header = new byte[12];

            await using var stream = System.IO.File.OpenRead(path);
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

            if (read < 12)
                return false;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return true;

            // WEBP: "RIFF" .... "WEBP"
            if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            // ICO: reserved=0, type=1, at least one image
            if (header[0] == 0 && header[1] == 0 && header[2] == 1 && header[3] == 0 && (header[4] != 0 || header[5] != 0))
                return true;

            return false;
        }
    }
}
