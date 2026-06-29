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

        public AdminNotificationsController(
            IAdminNotificationReadService service,
            INotificationService notificationService)
        {
            _service = service;
            _notificationService = notificationService;
        }
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminNotificationDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetAllAsync(filter);
            return Ok(ApiResult<List<AdminNotificationDto>>.Success(result, "اطلاعیه‌ها با موفقیت دریافت شدند."));
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

        [HttpPost("send")]
        public async Task<ActionResult<ApiResult>> Send(SendNotificationRequestDto request)
        {
            await _notificationService.SendSystemNotificationAsync(
                request.UserId,
                request.Title,
                request.Message);

            return Ok(ApiResult.Success("اعلان برای کاربر ارسال شد."));
        }
    }
}
