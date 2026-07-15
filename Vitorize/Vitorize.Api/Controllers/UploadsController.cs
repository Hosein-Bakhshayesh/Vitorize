using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.Shared.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Exceptions;

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
        private readonly ICurrentUserService _currentUser;

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

        public UploadsController(IWebHostEnvironment environment, ICurrentUserService currentUser)
        {
            _environment = environment;
            _currentUser = currentUser;
        }

        [HttpPost("verification-document")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadVerificationDocument(
            IFormFile file)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedException("کاربر احراز هویت نشده است.");
            var result = await Vitorize.Api.Controllers.Admin.UploadHelper.SavePrivateImageAsync(
                _environment, file, userId.ToString("N"), MaxFileSize, AllowedExtensions, AllowedContentTypes);

            return Ok(ApiResult<UploadFileResultDto>.Success(
                result,
                "مدرک با موفقیت آپلود شد."));
        }

        private Task<UploadFileResultDto> SaveImageAsync(IFormFile file, string folderName)
        {
            return Vitorize.Api.Controllers.Admin.UploadHelper.SaveImageAsync(
                _environment, file, folderName, MaxFileSize, AllowedExtensions, AllowedContentTypes);
        }
    }
}
