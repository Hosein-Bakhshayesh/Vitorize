using Vitorize.Application.DTOs.Admin.Content;

namespace Vitorize.Application.Interfaces
{
    /// <summary>Admin management of storefront blog articles.</summary>
    public interface IAdminBlogService
    {
        Task<List<AdminBlogPostListItemDto>> GetAllAsync();

        Task<AdminBlogPostDto> GetByIdAsync(Guid id);

        Task<AdminBlogPostDto> CreateAsync(CreateBlogPostRequestDto request);

        Task<AdminBlogPostDto> UpdateAsync(Guid id, UpdateBlogPostRequestDto request);

        Task<AdminBlogPostDto> SetPublishedAsync(Guid id, bool isPublished);

        Task DeleteAsync(Guid id);
    }
}
