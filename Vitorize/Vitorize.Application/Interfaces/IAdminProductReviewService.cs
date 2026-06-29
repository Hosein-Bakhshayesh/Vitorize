using Vitorize.Application.DTOs.Admin.Reviews;
using Vitorize.Shared.Common;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminProductReviewService
    {
        Task<PagedResult<AdminProductReviewDto>> GetAllAsync(
            AdminProductReviewFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminProductReviewDto> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<AdminProductReviewDto> ApproveAsync(
            Guid adminUserId,
            Guid id,
            CancellationToken cancellationToken = default);

        Task<AdminProductReviewDto> RejectAsync(
            Guid adminUserId,
            Guid id,
            RejectProductReviewRequestDto request,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Guid adminUserId,
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
