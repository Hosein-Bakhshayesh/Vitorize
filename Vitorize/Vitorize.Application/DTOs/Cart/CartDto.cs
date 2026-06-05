namespace Vitorize.Application.DTOs.Cart
{
    public class CartDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public List<CartItemDto> Items { get; set; } = new();

        public int TotalQuantity { get; set; }

        public decimal SubtotalAmount { get; set; }
    }
}