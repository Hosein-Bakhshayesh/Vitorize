namespace Vitorize.Application.DTOs.Storefront
{
    public class StorefrontProductDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? RedirectUrl { get; set; }

        public string? ThumbnailImagePath { get; set; }

        public decimal BasePrice { get; set; }

        public decimal? DiscountPrice { get; set; }

        public byte CurrencyType { get; set; }

        public bool IsFeatured { get; set; }

        public string CategoryTitle { get; set; } = string.Empty;

        public bool HasVariants { get; set; }

        // The home page reuses the normal product card. These values let that card apply the exact
        // same availability rule as /shop instead of interpreting omitted fields as zero stock.
        public bool ForceOutOfStock { get; set; }

        public bool IsUnlimitedStock { get; set; }

        public byte DeliveryType { get; set; }

        public int AvailableStock { get; set; }

        // Home.razor uses this to avoid a review request for products that have no public review.
        // It must travel with featured products as well as ordinary /products results; otherwise a
        // featured product's approved reviews are silently skipped on the homepage.
        public int ReviewCount { get; set; }

        public double AverageRating { get; set; }

    }
}
