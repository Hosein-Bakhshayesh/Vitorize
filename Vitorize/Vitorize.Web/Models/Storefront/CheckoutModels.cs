namespace Vitorize.Web.Models.Storefront
{
    public class StoreCheckoutRequestModel
    {
        public string? Description { get; set; }

        public string? CouponCode { get; set; }
    }

    public class StoreCheckoutResultModel
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

    public class StorePaymentStartResultModel
    {
        public Guid PaymentId { get; set; }

        public Guid OrderId { get; set; }

        public string PaymentUrl { get; set; } = string.Empty;

        public string? Authority { get; set; }

        public decimal Amount { get; set; }
    }
}