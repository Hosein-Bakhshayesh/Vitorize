using Vitorize.Application.DTOs.Admin.Products;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminProductService
    {
        Task<List<AdminProductDto>> GetAllAsync();

        Task<AdminProductDto> GetByIdAsync(Guid id);

        Task<AdminProductDto> CreateAsync(CreateProductRequestDto request);

        Task<AdminProductDto> UpdateAsync(Guid id, UpdateProductRequestDto request);

        Task DeleteAsync(Guid id);
    }
}