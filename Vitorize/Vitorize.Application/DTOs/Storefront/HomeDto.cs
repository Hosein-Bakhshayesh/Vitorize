namespace Vitorize.Application.DTOs.Storefront
{
    public class HomeDto
    {
        public List<BannerDto> Banners { get; set; } = new();

        public List<StorefrontCategoryDto> Categories { get; set; } = new();

        public List<StorefrontBrandDto> Brands { get; set; } = new();

        public List<StorefrontProductDto> FeaturedProducts { get; set; } = new();

        public List<StorefrontBlogPostDto> LatestBlogPosts { get; set; } = new();

        public List<FaqDto> Faqs { get; set; } = new();
    }
}