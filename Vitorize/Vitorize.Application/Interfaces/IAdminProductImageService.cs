using Vitorize.Application.DTOs.Admin.ProductImages;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminProductImageService
    {
        Task<List<AdminProductImageDto>> GetByProductIdAsync(Guid productId);
        Task<Vitorize.Shared.Common.PagedResult<AdminProductImageDto>> GetPagedByProductIdAsync(Guid productId, Vitorize.Application.DTOs.Admin.Products.ProductDetailFilterDto filter, CancellationToken cancellationToken = default);

        Task<AdminProductImageDto> CreateAsync(
            Guid productId,
            CreateProductImageRequestDto request);

        Task<AdminProductImageDto> UpdateAsync(
            Guid imageId,
            UpdateProductImageRequestDto request);

        Task SetAsThumbnailAsync(Guid imageId);

        Task DeleteAsync(Guid imageId);
    }
}
