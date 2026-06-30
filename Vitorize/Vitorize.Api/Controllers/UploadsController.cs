using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    /// <summary>
    /// آپلود فایل برای کاربران احراز هویت‌شده (مثلاً مدارک KYC).
    /// خروجی مسیر نسبی فایل ذخیره‌شده روی میزبان API است.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/uploads")]
    public class UploadsController : ControllerBase
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

        private const long MaxFileSize = 5 * 1024 * 1024;

        public UploadsController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("verification-document")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadVerificationDocument(
            IFormFile file)
        {
            var result = await SaveImageAsync(file, "verifications");

            return Ok(ApiResult<UploadFileResultDto>.Success(
                result,
                "مدرک با موفقیت آپلود شد."));
        }

        private async Task<UploadFileResultDto> SaveImageAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new Exception("فایل ارسال نشده است.");

            if (file.Length > MaxFileSize)
                throw new Exception("حجم فایل نباید بیشتر از ۵ مگابایت باشد.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                throw new Exception("فرمت فایل مجاز نیست.");

            if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new Exception("نوع فایل معتبر نیست.");

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", folderName);

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
