namespace Vitorize.Application.DTOs.Storefront
{
    public class StorefrontProductDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? ThumbnailImagePath { get; set; }

        public decimal BasePrice { get; set; }

        public decimal? DiscountPrice { get; set; }

        public bool IsFeatured { get; set; }

        public bool RequiresVerification { get; set; }
    }
}