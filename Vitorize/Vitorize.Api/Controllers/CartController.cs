using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly Vitorize.Api.Services.CartIdentityResolver _identityResolver;
        private readonly ILogger<CartController> _logger;
        private readonly Vitorize.Api.Services.TestingCartFaultService? _testingCartFaults;

        public CartController(
            ICartService cartService,
            Vitorize.Api.Services.CartIdentityResolver identityResolver,
            ILogger<CartController> logger,
            Vitorize.Api.Services.TestingCartFaultService? testingCartFaults = null)
        {
            _cartService = cartService;
            _identityResolver = identityResolver;
            _logger = logger;
            _testingCartFaults = testingCartFaults;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> Get()
        {
            if (_testingCartFaults?.ConsumeCartReadFailure() == true)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResult<CartDto>.Failure("بارگذاری سبد خرید موقتاً در دسترس نیست."));
            var result = await _cartService.GetAsync(_identityResolver.Resolve());

            return Ok(ApiResult<CartDto>.Success(
                result,
                "سبد خرید با موفقیت دریافت شد."));
        }

        [HttpPost("items")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> AddItem(
            AddToCartRequestDto request)
        {
            var result = await _cartService.AddItemAsync(_identityResolver.Resolve(), request);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "محصول با موفقیت به سبد خرید اضافه شد."));
        }

        [HttpPut("items/{cartItemId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> UpdateItem(
            Guid cartItemId,
            UpdateCartItemRequestDto request)
        {
            var result = await _cartService.UpdateItemAsync(
                _identityResolver.Resolve(),
                cartItemId,
                request);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "آیتم سبد خرید با موفقیت بروزرسانی شد."));
        }

        [HttpDelete("items/{cartItemId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> RemoveItem(
            Guid cartItemId)
        {
            var result = await _cartService.RemoveItemAsync(
                _identityResolver.Resolve(),
                cartItemId);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "آیتم از سبد خرید حذف شد."));
        }

        [HttpDelete("clear")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult>> Clear()
        {
            await _cartService.ClearAsync(_identityResolver.Resolve());

            return Ok(ApiResult.Success("سبد خرید با موفقیت خالی شد."));
        }

        [HttpPost("merge-guest")]
        public async Task<ActionResult<ApiResult<CartDto>>> MergeGuest([FromBody] MergeGuestCartRequest request)
        {
            var user = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(user, out var userId)) throw new UnauthorizedException("کاربر احراز هویت نشده است.");
            var result = await _cartService.MergeGuestCartAsync(userId, request.GuestToken);
            _logger.LogInformation("GuestCartMerged UserId={UserId} EventType={EventType}", userId, "GuestCartMerged");
            return Ok(ApiResult<CartDto>.Success(result, "سبد خرید مهمان با موفقیت منتقل شد."));
        }

        public sealed record MergeGuestCartRequest(string GuestToken);
    }
}
