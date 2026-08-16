using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Helpers;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    /// <summary>
    /// FIX-15 group announcements. Guarded by <c>UserManage</c> rather than <c>AdminOnly</c>:
    /// a broadcast reaches the entire customer base, so it is restricted to the strongest existing
    /// user-scope permission. The single-recipient endpoint keeps its own authorization.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "UserManage")]
    [Route("api/admin/notification-broadcasts")]
    public class AdminNotificationBroadcastsController : ControllerBase
    {
        private readonly IAdminNotificationBroadcastService _broadcastService;
        private readonly IIdempotencyService _idempotencyService;
        private readonly ICurrentUserService _currentUserService;

        public AdminNotificationBroadcastsController(
            IAdminNotificationBroadcastService broadcastService,
            IIdempotencyService idempotencyService,
            ICurrentUserService currentUserService)
        {
            _broadcastService = broadcastService;
            _idempotencyService = idempotencyService;
            _currentUserService = currentUserService;
        }

        [HttpPost("preview")]
        public async Task<ActionResult<ApiResult<BroadcastPreviewResultDto>>> Preview(
            BroadcastPreviewRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _broadcastService.PreviewAsync(request, cancellationToken);

            return Ok(ApiResult<BroadcastPreviewResultDto>.Success(result, "تعداد گیرندگان محاسبه شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<BroadcastDto>>> Send(
            [FromBody] SendBroadcastRequestDto request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new BusinessException("Idempotency-Key الزامی است.");

            var actorUserId = _currentUserService.UserId.Value;

            // Deterministic hash over the whole payload, with the selection sorted and
            // deduplicated so an identical send is recognised regardless of client ordering.
            var requestHash = RequestHashHelper.ComputeHash(new
            {
                request.Audience,
                SelectedCustomerIds = (request.SelectedCustomerIds ?? new List<Guid>())
                    .Distinct().OrderBy(x => x).ToList(),
                Title = request.Title?.Trim(),
                Message = request.Message?.Trim(),
                ActionUrl = request.ActionUrl?.Trim()
            });

            await _idempotencyService.StartAsync(actorUserId, idempotencyKey, requestHash);

            try
            {
                var result = await _broadcastService.SendAsync(actorUserId, request, cancellationToken);

                var response = ApiResult<BroadcastDto>.Success(
                    result,
                    $"اعلان برای {result.RecipientCount} کاربر ارسال شد.");

                await _idempotencyService.CompleteAsync(
                    idempotencyKey,
                    JsonSerializer.Serialize(response),
                    StatusCodes.Status200OK);

                return Ok(response);
            }
            catch (Exception ex)
            {
                await _idempotencyService.FailAsync(idempotencyKey, ex.Message);
                throw;
            }
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<PagedResult<BroadcastDto>>>> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await _broadcastService.GetHistoryAsync(page, pageSize, cancellationToken);

            return Ok(ApiResult<PagedResult<BroadcastDto>>.Success(result, "تاریخچه ارسال‌های گروهی دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<BroadcastDto>>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var result = await _broadcastService.GetByIdAsync(id, cancellationToken);

            return Ok(ApiResult<BroadcastDto>.Success(result, "جزئیات ارسال گروهی دریافت شد."));
        }
    }
}
