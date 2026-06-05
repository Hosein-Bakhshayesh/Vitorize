namespace Vitorize.Application.DTOs.Cart
{
    public class CartItemDto
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public string ProductTitle { get; set; } = string.Empty;

        public string? VariantTitle { get; set; }

        public string? ThumbnailImagePath { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }
    }
}