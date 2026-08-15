using Vitorize.Application.DTOs.Admin.Content;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminPageService
    {
        Task<List<AdminPageListItemDto>> GetAllAsync();

        Task<AdminPageDto> GetByIdAsync(Guid id);

        Task<AdminPageDto> CreateAsync(CreatePageRequestDto request);

        Task<AdminPageDto> UpdateAsync(Guid id, UpdatePageRequestDto request);

        Task<AdminPageDto> SetPublishedAsync(Guid id, bool isPublished);

        Task DeleteAsync(Guid id);
    }
}
