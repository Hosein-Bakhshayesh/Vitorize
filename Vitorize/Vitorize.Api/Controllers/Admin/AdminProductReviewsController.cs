using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Reviews;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/product-reviews")]
    public class AdminProductReviewsController : ControllerBase
    {
        private readonly IAdminProductReviewService _reviewService;
        private readonly ICurrentUserService _currentUserService;

        public AdminProductReviewsController(
            IAdminProductReviewService reviewService,
            ICurrentUserService currentUserService)
        {
            _reviewService = reviewService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<PagedResult<AdminProductReviewDto>>>> GetAll(
            [FromQuery] AdminProductReviewFilterDto filter,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetAllAsync(filter, cancellationToken);

            return Ok(ApiResult<PagedResult<AdminProductReviewDto>>.Success(
                result,
                "لیست نظرات با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminProductReviewDto>>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetByIdAsync(id, cancellationToken);

            return Ok(ApiResult<AdminProductReviewDto>.Success(
                result,
                "جزئیات نظر با موفقیت دریافت شد."));
        }

        [HttpPost("{id:guid}/approve")]
        public async Task<ActionResult<ApiResult<AdminProductReviewDto>>> Approve(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.ApproveAsync(GetAdminId(), id, cancellationToken);

            return Ok(ApiResult<AdminProductReviewDto>.Success(
                result,
                "نظر با موفقیت تأیید شد."));
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<ActionResult<ApiResult<AdminProductReviewDto>>> Reject(
            Guid id,
            RejectProductReviewRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.RejectAsync(
                GetAdminId(),
                id,
                request,
                cancellationToken);

            return Ok(ApiResult<AdminProductReviewDto>.Success(
                result,
                "نظر با موفقیت رد شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(
            Guid id,
            CancellationToken cancellationToken)
        {
            await _reviewService.DeleteAsync(GetAdminId(), id, cancellationToken);

            return Ok(ApiResult.Success("نظر با موفقیت حذف شد."));
        }

        private Guid GetAdminId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}
