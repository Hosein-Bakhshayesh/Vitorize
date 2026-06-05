using Vitorize.Application.DTOs.Admin.ProductVariants;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminProductVariantService
    {
        Task<List<AdminProductVariantDto>> GetByProductIdAsync(Guid productId);

        Task<AdminProductVariantDto> GetByIdAsync(Guid id);

        Task<AdminProductVariantDto> CreateAsync(
            Guid productId,
            CreateProductVariantRequestDto request);

        Task<AdminProductVariantDto> UpdateAsync(
            Guid id,
            UpdateProductVariantRequestDto request);

        Task DeleteAsync(Guid id);
    }
}