using Vitorize.Application.DTOs.Admin.Categories;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminCategoryService
    {
        Task<List<AdminCategoryDto>> GetAllAsync();
        Task<AdminCategoryDto> GetByIdAsync(Guid id);
        Task<AdminCategoryDto> CreateAsync(CreateCategoryRequestDto request);
        Task<AdminCategoryDto> UpdateAsync(Guid id, UpdateCategoryRequestDto request);
        Task DeleteAsync(Guid id);
    }
}