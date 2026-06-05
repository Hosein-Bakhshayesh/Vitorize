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
        private readonly ICurrentUserService _currentUserService;

        public CartController(
            ICartService cartService,
            ICurrentUserService currentUserService)
        {
            _cartService = cartService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<CartDto>>> Get()
        {
            var userId = GetUserId();

            var result = await _cartService.GetAsync(userId);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "سبد خرید با موفقیت دریافت شد."));
        }

        [HttpPost("items")]
        public async Task<ActionResult<ApiResult<CartDto>>> AddItem(
            AddToCartRequestDto request)
        {
            var userId = GetUserId();

            var result = await _cartService.AddItemAsync(userId, request);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "محصول با موفقیت به سبد خرید اضافه شد."));
        }

        [HttpPut("items/{cartItemId:guid}")]
        public async Task<ActionResult<ApiResult<CartDto>>> UpdateItem(
            Guid cartItemId,
            UpdateCartItemRequestDto request)
        {
            var userId = GetUserId();

            var result = await _cartService.UpdateItemAsync(
                userId,
                cartItemId,
                request);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "آیتم سبد خرید با موفقیت بروزرسانی شد."));
        }

        [HttpDelete("items/{cartItemId:guid}")]
        public async Task<ActionResult<ApiResult<CartDto>>> RemoveItem(
            Guid cartItemId)
        {
            var userId = GetUserId();

            var result = await _cartService.RemoveItemAsync(
                userId,
                cartItemId);

            return Ok(ApiResult<CartDto>.Success(
                result,
                "آیتم از سبد خرید حذف شد."));
        }

        [HttpDelete("clear")]
        public async Task<ActionResult<ApiResult>> Clear()
        {
            var userId = GetUserId();

            await _cartService.ClearAsync(userId);

            return Ok(ApiResult.Success("سبد خرید با موفقیت خالی شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}