using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ICurrentUserService _currentUserService;

        public CheckoutController(
            ICheckoutService checkoutService,
            ICurrentUserService currentUserService)
        {
            _checkoutService = checkoutService;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<CheckoutResultDto>>> Checkout(
            CheckoutRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var result = await _checkoutService.CheckoutAsync(
                _currentUserService.UserId.Value,
                request);

            return Ok(
                ApiResult<CheckoutResultDto>.Success(
                    result,
                    "سفارش با موفقیت ایجاد شد."));
        }
    }
}