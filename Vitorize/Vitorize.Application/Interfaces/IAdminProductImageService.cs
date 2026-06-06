using Vitorize.Application.DTOs.Admin.ProductImages;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminProductImageService
    {
        Task<List<AdminProductImageDto>> GetByProductIdAsync(Guid productId);

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