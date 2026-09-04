using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.Shared.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Exceptions;
using Vitorize.Api.Hosting;
using Vitorize.Infrastructure.Persistence;

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
        private readonly HostingStoragePaths _storagePaths;
        private readonly VitorizeDbContext _db;
        private readonly IOrderItemKycDeadlineService _kycDeadlines;

        private static readonly string[] AllowedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/jpg",
            "image/pjpeg",
            "image/png",
            "image/webp"
        };

        private const long MaxFileSize = 5 * 1024 * 1024;

        public UploadsController(
            IWebHostEnvironment environment,
            ICurrentUserService currentUser,
            HostingStoragePaths storagePaths,
            VitorizeDbContext db,
            IOrderItemKycDeadlineService kycDeadlines)
        {
            _environment = environment;
            _currentUser = currentUser;
            _storagePaths = storagePaths;
            _db = db;
            _kycDeadlines = kycDeadlines;
        }

        [HttpPost("verification-document")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<ActionResult<ApiResult<UploadFileResultDto>>> UploadVerificationDocument(
            IFormFile file,
            [FromQuery] Guid? orderItemId)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedException("کاربر احراز هویت نشده است.");
            // The private file must not be persisted once the item's customer-action
            // deadline has elapsed. The order ownership check also prevents a caller
            // from using another customer's item id as an expiry trigger.
            if (orderItemId.HasValue)
            {
                var ownsItem = await _db.OrderItems
                    .AnyAsync(item => item.Id == orderItemId.Value && item.Order.UserId == userId);
                if (!ownsItem)
                    throw new NotFoundException("Order item was not found.");

                if (await _kycDeadlines.ExpireIfOverdueAsync(orderItemId.Value, HttpContext.RequestAborted))
                    throw new ConcurrencyConflictException("The customer-action deadline for this item has expired.");

                // ExpireIfOverdueAsync returns false after a previous command has
                // already converged the row.  Do not let that idempotent result
                // turn into an upload bypass for Expired, AwaitingReview, or a
                // terminal KYC state.
                var lifecycleStatus = await _db.OrderItemKycStates
                    .Where(state => state.OrderItemId == orderItemId.Value)
                    .Select(state => (byte?)state.Status)
                    .SingleOrDefaultAsync(HttpContext.RequestAborted);
                if (lifecycleStatus is not ((byte)Vitorize.Shared.Enums.OrderItemKycStatus.AwaitingSubmission or
                    (byte)Vitorize.Shared.Enums.OrderItemKycStatus.Rejected))
                    throw new ConcurrencyConflictException("The selected order item is not accepting customer verification documents.");
            }

            var result = await Vitorize.Api.Controllers.Admin.UploadHelper.SavePrivateImageAsync(
                _storagePaths.PrivateDocumentsRoot, file, userId.ToString("N"), MaxFileSize, AllowedExtensions, AllowedContentTypes);

            return Ok(ApiResult<UploadFileResultDto>.Success(
                result,
                "مدرک با موفقیت آپلود شد."));
        }

        private Task<UploadFileResultDto> SaveImageAsync(IFormFile file, string folderName)
        {
            return Vitorize.Api.Controllers.Admin.UploadHelper.SaveImageAsync(
                _storagePaths.PublicMediaRoot, file, folderName, MaxFileSize, AllowedExtensions, AllowedContentTypes);
        }
    }
}
