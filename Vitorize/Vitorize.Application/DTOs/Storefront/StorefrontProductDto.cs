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

        // The home page reuses the normal product card. These values let that card apply the exact
        // same availability rule as /shop instead of interpreting omitted fields as zero stock.
        public bool ForceOutOfStock { get; set; }

        public bool IsUnlimitedStock { get; set; }

        public byte DeliveryType { get; set; }

        public int AvailableStock { get; set; }

    }
}
