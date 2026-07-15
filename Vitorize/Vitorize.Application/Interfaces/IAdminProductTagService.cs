using Vitorize.Application.DTOs.Admin.Products;

namespace Vitorize.Application.Interfaces;

public interface IAdminProductTagService
{
    Task<List<AdminProductTagDto>> GetAllAsync();
    Task<AdminProductTagDto> CreateAsync(SaveProductTagRequestDto request);
    Task<AdminProductTagDto> UpdateAsync(Guid id, SaveProductTagRequestDto request);
    Task DeleteAsync(Guid id);
}
