namespace Vitorize.Application.DTOs.Checkout
{
    public class CheckoutResultDto
    {
        public Guid OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public decimal SubtotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal FinalAmount { get; set; }

        public byte OrderStatus { get; set; }

        public byte PaymentStatus { get; set; }

        public List<Guid> ReservationIds { get; set; } = new();
    }
}