namespace Vitorize.Application.DTOs.Cart
{
    public class AddToCartRequestDto
    {
        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public int Quantity { get; set; } = 1;
        public Dictionary<string, string?> InputValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
