using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
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

        private const long MaxFileSize = 2 * 1024 * 1024;

        public AdminUploadsController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("product-image")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadProductImage(
            IFormFile file)
        {
            var result = await UploadImageAsync(file, "products");

            return Ok(ApiResult<UploadFileResultDto>.Success(
                result,
                "تصویر محصول با موفقیت آپلود شد."));
        }

        private async Task<UploadFileResultDto> UploadImageAsync(
            IFormFile file,
            string folderName)
        {
            if (file == null || file.Length == 0)
                throw new Exception("فایل تصویر ارسال نشده است.");

            if (file.Length > MaxFileSize)
                throw new Exception("حجم تصویر نباید بیشتر از ۲ مگابایت باشد.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                throw new Exception("فرمت تصویر مجاز نیست.");

            if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new Exception("نوع فایل معتبر نیست.");

            var uploadRoot = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                folderName);

            if (!Directory.Exists(uploadRoot))
                Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            var relativePath = $"/uploads/{folderName}/{fileName}";

            return new UploadFileResultDto
            {
                FileName = fileName,
                FilePath = relativePath,
                ContentType = file.ContentType,
                Size = file.Length
            };
        }
    }
}