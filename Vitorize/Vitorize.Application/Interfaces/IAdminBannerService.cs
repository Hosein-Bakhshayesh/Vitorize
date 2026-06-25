using Vitorize.Application.DTOs.Admin.Banners;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminBannerService
    {
        Task<List<AdminBannerDto>> GetAllAsync();
        Task<AdminBannerDto> GetByIdAsync(Guid id);
        Task<AdminBannerDto> CreateAsync(CreateBannerRequestDto request);
        Task<AdminBannerDto> UpdateAsync(Guid id, UpdateBannerRequestDto request);
        Task DeleteAsync(Guid id);
    }
}
