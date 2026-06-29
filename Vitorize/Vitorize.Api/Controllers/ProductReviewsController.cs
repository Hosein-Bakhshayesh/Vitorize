using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Reviews;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/product-reviews")]
    public class ProductReviewsController : ControllerBase
    {
        private readonly IProductReviewService _reviewService;
        private readonly ICurrentUserService _currentUserService;

        public ProductReviewsController(
            IProductReviewService reviewService,
            ICurrentUserService currentUserService)
        {
            _reviewService = reviewService;
            _currentUserService = currentUserService;
        }

        [HttpGet("product/{productId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<ProductReviewListResultDto>>> GetForProduct(
            Guid productId,
            [FromQuery] ProductReviewFilterDto filter,
            CancellationToken cancellationToken)
        {
            filter.ProductId = productId;

            var result = await _reviewService.GetApprovedForProductAsync(
                filter,
                _currentUserService.UserId,
                cancellationToken);

            return Ok(ApiResult<ProductReviewListResultDto>.Success(
                result,
                "نظرات محصول با موفقیت دریافت شد."));
        }

        [HttpGet("product/{productId:guid}/summary")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResult<ProductReviewSummaryDto>>> GetSummary(
            Guid productId,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetSummaryAsync(productId, cancellationToken);

            return Ok(ApiResult<ProductReviewSummaryDto>.Success(
                result,
                "خلاصه نظرات محصول با موفقیت دریافت شد."));
        }

        [HttpGet("mine")]
        [Authorize]
        public async Task<ActionResult<ApiResult<List<ProductReviewDto>>>> GetMine(
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetMyReviewsAsync(GetUserId(), cancellationToken);

            return Ok(ApiResult<List<ProductReviewDto>>.Success(
                result,
                "نظرات شما با موفقیت دریافت شد."));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ApiResult<ProductReviewDto>>> Create(
            CreateProductReviewRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.CreateAsync(GetUserId(), request, cancellationToken);

            return Ok(ApiResult<ProductReviewDto>.Success(
                result,
                "نظر شما ثبت شد و پس از بررسی منتشر می‌شود."));
        }

        [HttpPut("{reviewId:guid}")]
        [Authorize]
        public async Task<ActionResult<ApiResult<ProductReviewDto>>> Update(
            Guid reviewId,
            UpdateProductReviewRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.UpdateAsync(
                GetUserId(),
                reviewId,
                request,
                cancellationToken);

            return Ok(ApiResult<ProductReviewDto>.Success(
                result,
                "نظر شما ویرایش شد و مجدداً بررسی می‌شود."));
        }

        [HttpDelete("{reviewId:guid}")]
        [Authorize]
        public async Task<ActionResult<ApiResult>> Delete(
            Guid reviewId,
            CancellationToken cancellationToken)
        {
            await _reviewService.DeleteAsync(GetUserId(), reviewId, cancellationToken);

            return Ok(ApiResult.Success("نظر شما حذف شد."));
        }

        [HttpPost("{reviewId:guid}/vote")]
        [Authorize]
        public async Task<ActionResult<ApiResult<ProductReviewDto>>> Vote(
            Guid reviewId,
            ProductReviewVoteRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.VoteAsync(
                GetUserId(),
                reviewId,
                request.VoteType,
                cancellationToken);

            return Ok(ApiResult<ProductReviewDto>.Success(
                result,
                "رأی شما ثبت شد."));
        }

        [HttpDelete("{reviewId:guid}/vote")]
        [Authorize]
        public async Task<ActionResult<ApiResult>> RemoveVote(
            Guid reviewId,
            CancellationToken cancellationToken)
        {
            await _reviewService.RemoveVoteAsync(GetUserId(), reviewId, cancellationToken);

            return Ok(ApiResult.Success("رأی شما حذف شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}
