using Vitorize.Application.DTOs.Reviews;

namespace Vitorize.Application.Interfaces
{
    public interface IProductReviewService
    {
        /// <summary>
        /// لیست عمومی نظرات تأییدشده یک محصول به همراه خلاصه امتیازها.
        /// در صورت احراز هویت، رأی کاربر فعلی روی هر نظر نیز برگردانده می‌شود.
        /// </summary>
        Task<ProductReviewListResultDto> GetApprovedForProductAsync(
            ProductReviewFilterDto filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<ProductReviewSummaryDto> GetSummaryAsync(
            Guid productId,
            CancellationToken cancellationToken = default);

        Task<List<ProductReviewDto>> GetMyReviewsAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<ProductReviewDto> CreateAsync(
            Guid userId,
            CreateProductReviewRequestDto request,
            CancellationToken cancellationToken = default);

        Task<ProductReviewDto> UpdateAsync(
            Guid userId,
            Guid reviewId,
            UpdateProductReviewRequestDto request,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Guid userId,
            Guid reviewId,
            CancellationToken cancellationToken = default);

        Task<ProductReviewDto> VoteAsync(
            Guid userId,
            Guid reviewId,
            byte voteType,
            CancellationToken cancellationToken = default);

        Task RemoveVoteAsync(
            Guid userId,
            Guid reviewId,
            CancellationToken cancellationToken = default);
    }
}
