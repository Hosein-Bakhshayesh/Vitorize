using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/notifications")]
    public class AdminNotificationsController : ControllerBase
    {
        private readonly IAdminNotificationReadService _service;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUser;

        public AdminNotificationsController(
            IAdminNotificationReadService service,
            INotificationService notificationService,
            ICurrentUserService currentUser)
        {
            _service = service;
            _notificationService = notificationService;
            _currentUser = currentUser;
        }
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminNotificationDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetAllAsync(filter);
            return Ok(ApiResult<List<AdminNotificationDto>>.Success(result, "اطلاعیه‌ها با موفقیت دریافت شدند."));
        }
        [HttpGet("paged")]
        public async Task<ActionResult<ApiResult<PagedResult<AdminNotificationDto>>>> GetPaged([FromQuery] AdminQueryFilterDto filter, CancellationToken cancellationToken)
        {
            var result = await _service.GetPagedAsync(filter, cancellationToken);
            return Ok(ApiResult<PagedResult<AdminNotificationDto>>.Success(result, "فهرست صفحه‌بندی‌شده اطلاعیه‌ها دریافت شد."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminNotificationDto>>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResult<AdminNotificationDto>.Success(result, "جزئیات اطلاعیه با موفقیت دریافت شد."));
        }
        [HttpPost("{id:guid}/read")]
        public async Task<ActionResult<ApiResult>> MarkAsRead(Guid id)
        {
            await _service.MarkAsReadAsync(id);
            return Ok(ApiResult.Success("اطلاعیه خوانده شد."));
        }

        [HttpGet("kyc-reminder-recipients")]
        public async Task<ActionResult<ApiResult<List<KycReminderRecipientDto>>>> GetKycReminderRecipients(CancellationToken cancellationToken)
        {
            var result = await _service.GetKycReminderRecipientsAsync(cancellationToken);
            return Ok(ApiResult<List<KycReminderRecipientDto>>.Success(result, "فهرست کاربران نیازمند احراز هویت دریافت شد."));
        }

        [HttpPost("send")]
        public async Task<ActionResult<ApiResult>> Send(SendNotificationRequestDto request)
        {
            await _notificationService.SendSystemNotificationAsync(
                request.UserId,
                request.Title,
                request.Message,
                request.SendSms,
                _currentUser.UserId);

            return Ok(ApiResult.Success(request.SendSms
                ? "اعلان ثبت و پیامک در صف ارسال قرار گرفت."
                : "اعلان برای کاربر ارسال شد."));
        }

        [HttpPost("kyc-reminder")]
        public async Task<ActionResult<ApiResult>> SendKycReminder(SendNotificationRequestDto request, CancellationToken cancellationToken)
        {
            await _notificationService.SendKycReminderAsync(
                request.UserId,
                request.Title,
                request.Message,
                request.SendSms,
                _currentUser.UserId,
                cancellationToken);

            return Ok(ApiResult.Success(request.SendSms
                ? "یادآوری احراز هویت ثبت و پیامک در صف ارسال قرار گرفت."
                : "یادآوری احراز هویت برای کاربر ارسال شد."));
        }
    }
}
