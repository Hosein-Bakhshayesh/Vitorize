using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;
        private readonly ICurrentUserService _currentUserService;

        public CouponsController(
            ICouponService couponService,
            ICurrentUserService currentUserService)
        {
            _couponService = couponService;
            _currentUserService = currentUserService;
        }

        [HttpPost("validate")]
        public async Task<ActionResult<ApiResult<ValidateCouponResultDto>>> Validate(
            ValidateCouponRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var result = await _couponService.ValidateAsync(
                _currentUserService.UserId.Value,
                request);

            return Ok(ApiResult<ValidateCouponResultDto>.Success(
                result,
                "کد تخفیف با موفقیت اعمال شد."));
        }
    }
}