using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Vitorize.Api.Services.TestingCartFaultService? _testingCartFaults;

        public CartController(
            ICartService cartService,
            Vitorize.Api.Services.CartIdentityResolver identityResolver,
            ILogger<CartController> logger,
            IServiceScopeFactory scopeFactory,
            Vitorize.Api.Services.TestingCartFaultService? testingCartFaults = null)
        {
            _cartService = cartService;
            _identityResolver = identityResolver;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _testingCartFaults = testingCartFaults;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> Get()
        {
            if (_testingCartFaults?.ConsumeCartReadFailure() == true)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResult<CartDto>.Failure("بارگذاری سبد خرید موقتاً در دسترس نیست."));
            var identity = _identityResolver.Resolve();
            var result = await ExecuteWithDeadlockRetryAsync("Cart.Get", service => service.GetAsync(identity));

            return Ok(ApiResult<CartDto>.Success(
                result,
                "سبد خرید با موفقیت دریافت شد."));
        }

        [HttpPost("items")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> AddItem(
            AddToCartRequestDto request)
        {
            var identity = _identityResolver.Resolve();
            var result = await ExecuteWithDeadlockRetryAsync("Cart.AddItem",
                service => service.AddItemAsync(identity, request));

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
            var identity = _identityResolver.Resolve();
            var result = await ExecuteWithDeadlockRetryAsync("Cart.UpdateItem",
                service => service.UpdateItemAsync(identity, cartItemId, request));

            return Ok(ApiResult<CartDto>.Success(
                result,
                "آیتم سبد خرید با موفقیت بروزرسانی شد."));
        }

        [HttpDelete("items/{cartItemId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<CartDto>>> RemoveItem(
            Guid cartItemId)
        {
            var identity = _identityResolver.Resolve();
            var result = await ExecuteWithDeadlockRetryAsync("Cart.RemoveItem",
                service => service.RemoveItemAsync(identity, cartItemId));

            return Ok(ApiResult<CartDto>.Success(
                result,
                "آیتم از سبد خرید حذف شد."));
        }

        [HttpDelete("clear")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult>> Clear()
        {
            var identity = _identityResolver.Resolve();
            await ExecuteWithDeadlockRetryAsync("Cart.Clear", async service =>
            {
                await service.ClearAsync(identity);
                return true;
            });

            return Ok(ApiResult.Success("سبد خرید با موفقیت خالی شد."));
        }

        [HttpPost("merge-guest")]
        public async Task<ActionResult<ApiResult<CartDto>>> MergeGuest([FromBody] MergeGuestCartRequest request)
        {
            var user = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(user, out var userId)) throw new UnauthorizedException("کاربر احراز هویت نشده است.");
            var result = await ExecuteWithDeadlockRetryAsync("Cart.MergeGuest",
                service => service.MergeGuestCartAsync(userId, request.GuestToken));
            _logger.LogInformation("GuestCartMerged UserId={UserId} EventType={EventType}", userId, "GuestCartMerged");
            return Ok(ApiResult<CartDto>.Success(result, "سبد خرید مهمان با موفقیت منتقل شد."));
        }

        private Task<T> ExecuteWithDeadlockRetryAsync<T>(string operationName, Func<ICartService, Task<T>> operation) =>
            Vitorize.Api.Services.SqlDeadlockRetry.ExecuteOnceAsync(
                () => operation(_cartService),
                async () =>
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    return await operation(scope.ServiceProvider.GetRequiredService<ICartService>());
                },
                _logger,
                operationName,
                HttpContext.RequestAborted);

        public sealed record MergeGuestCartRequest(string GuestToken);
    }
}
