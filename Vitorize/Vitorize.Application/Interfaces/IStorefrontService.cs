using Vitorize.Application.DTOs.Storefront;

namespace Vitorize.Application.Interfaces
{
    public interface IStorefrontService
    {
        Task<HomeDto> GetHomeAsync();

        /// <summary>
        /// بنرهای فعال (با رعایت بازه زمانی) مرتب‌شده بر اساس SortOrder.
        /// در صورت ارسال position فقط بنرهای همان جایگاه برگردانده می‌شوند.
        /// </summary>
        Task<List<BannerDto>> GetActiveBannersAsync(string? position);

        Task<List<FaqDto>> GetFaqsAsync();

        Task<PageDto> GetPageBySlugAsync(string slug);

        Task<List<StorefrontBlogPostDto>> GetBlogPostsAsync();

        Task<BlogPostDto> GetBlogPostBySlugAsync(string slug);
    }
}