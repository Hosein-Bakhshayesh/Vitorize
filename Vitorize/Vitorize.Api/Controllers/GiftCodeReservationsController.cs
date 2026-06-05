using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.GiftCodes;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GiftCodeReservationsController : ControllerBase
    {
        private readonly IGiftCodeReservationService _reservationService;
        private readonly ICurrentUserService _currentUserService;

        public GiftCodeReservationsController(
            IGiftCodeReservationService reservationService,
            ICurrentUserService currentUserService)
        {
            _reservationService = reservationService;
            _currentUserService = currentUserService;
        }

        [HttpPost("reserve")]
        public async Task<ActionResult<ApiResult<GiftCodeReservationDto>>> Reserve(
            ReserveGiftCodeRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var result = await _reservationService.ReserveAsync(
                _currentUserService.UserId.Value,
                request);

            return Ok(ApiResult<GiftCodeReservationDto>.Success(
                result,
                "کد با موفقیت رزرو شد."));
        }

        [HttpPost("{reservationId:guid}/release")]
        public async Task<ActionResult<ApiResult>> Release(Guid reservationId)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await _reservationService.ReleaseAsync(
                _currentUserService.UserId.Value,
                reservationId);

            return Ok(ApiResult.Success("رزرو با موفقیت آزاد شد."));
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("release-expired")]
        public async Task<ActionResult<ApiResult>> ReleaseExpired()
        {
            await _reservationService.ReleaseExpiredReservationsAsync();

            return Ok(ApiResult.Success("رزروهای منقضی شده آزاد شدند."));
        }
    }
}