using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Wishlist;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/wishlist")]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistService _wishlistService;
        private readonly ICurrentUserService _currentUserService;

        public WishlistController(
            IWishlistService wishlistService,
            ICurrentUserService currentUserService)
        {
            _wishlistService = wishlistService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<WishlistItemDto>>>> GetMyWishlist()
        {
            var userId = GetUserId();

            var result = await _wishlistService.GetMyWishlistAsync(userId);

            return Ok(ApiResult<List<WishlistItemDto>>.Success(
                result,
                "لیست علاقه‌مندی‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("product-ids")]
        public async Task<ActionResult<ApiResult<List<Guid>>>> GetMyWishlistProductIds()
        {
            var userId = GetUserId();

            var result = await _wishlistService.GetMyWishlistProductIdsAsync(userId);

            return Ok(ApiResult<List<Guid>>.Success(
                result,
                "شناسه محصولات علاقه‌مندی با موفقیت دریافت شد."));
        }

        [HttpGet("count")]
        public async Task<ActionResult<ApiResult<int>>> GetMyWishlistCount()
        {
            var userId = GetUserId();

            var result = await _wishlistService.GetMyWishlistCountAsync(userId);

            return Ok(ApiResult<int>.Success(
                result,
                "تعداد علاقه‌مندی‌ها با موفقیت دریافت شد."));
        }

        [HttpPost("{productId:guid}/toggle")]
        public async Task<ActionResult<ApiResult<bool>>> Toggle(Guid productId)
        {
            var userId = GetUserId();

            var added = await _wishlistService.ToggleAsync(userId, productId);

            return Ok(ApiResult<bool>.Success(
                added,
                added
                    ? "محصول به علاقه‌مندی‌ها اضافه شد."
                    : "محصول از علاقه‌مندی‌ها حذف شد."));
        }

        [HttpDelete("{productId:guid}")]
        public async Task<ActionResult<ApiResult>> Remove(Guid productId)
        {
            var userId = GetUserId();

            await _wishlistService.RemoveAsync(userId, productId);

            return Ok(ApiResult.Success(
                "محصول از علاقه‌مندی‌ها حذف شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}
