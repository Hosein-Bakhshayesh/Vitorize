using Vitorize.Application.DTOs.Admin.HomeSlides;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminHomeSlideService
    {
        Task<List<AdminHomeSlideDto>> GetAllAsync();
        Task<AdminHomeSlideDto> GetByIdAsync(Guid id);
        Task<AdminHomeSlideDto> CreateAsync(CreateHomeSlideRequestDto request);
        Task<AdminHomeSlideDto> UpdateAsync(Guid id, UpdateHomeSlideRequestDto request);
        Task DeleteAsync(Guid id);
    }
}