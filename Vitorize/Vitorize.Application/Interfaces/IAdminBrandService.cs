using Vitorize.Application.DTOs.Admin.Brands;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminBrandService
    {
        Task<List<AdminBrandDto>> GetAllAsync();

        Task<AdminBrandDto> GetByIdAsync(Guid id);

        Task<AdminBrandDto> CreateAsync(CreateBrandRequestDto request);

        Task<AdminBrandDto> UpdateAsync(Guid id, UpdateBrandRequestDto request);

        Task DeleteAsync(Guid id);
    }
}