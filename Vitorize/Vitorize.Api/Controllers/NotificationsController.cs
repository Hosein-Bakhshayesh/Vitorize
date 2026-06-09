using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;

        public NotificationsController(
            INotificationService notificationService,
            ICurrentUserService currentUserService)
        {
            _notificationService = notificationService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<NotificationDto>>>> Get()
        {
            var result = await _notificationService
                .GetMyNotificationsAsync(GetUserId());

            return Ok(ApiResult<List<NotificationDto>>.Success(
                result,
                "اعلان‌ها با موفقیت دریافت شدند."));
        }

        [HttpPost("{notificationId:guid}/read")]
        public async Task<ActionResult<ApiResult>> MarkAsRead(
            Guid notificationId)
        {
            await _notificationService.MarkAsReadAsync(
                GetUserId(),
                notificationId);

            return Ok(ApiResult.Success(
                "اعلان خوانده شد."));
        }

        [HttpPost("read-all")]
        public async Task<ActionResult<ApiResult>> MarkAllAsRead()
        {
            await _notificationService.MarkAllAsReadAsync(
                GetUserId());

            return Ok(ApiResult.Success(
                "همه اعلان‌ها خوانده شدند."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}