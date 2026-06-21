using Vitorize.Application.DTOs.Storefront;

namespace Vitorize.Application.Interfaces
{
    public interface IStorefrontService
    {
        Task<HomeDto> GetHomeAsync();

        Task<List<FaqDto>> GetFaqsAsync();

        Task<PageDto> GetPageBySlugAsync(string slug);

        Task<List<StorefrontBlogPostDto>> GetBlogPostsAsync();

        Task<BlogPostDto> GetBlogPostBySlugAsync(string slug);
    }
}