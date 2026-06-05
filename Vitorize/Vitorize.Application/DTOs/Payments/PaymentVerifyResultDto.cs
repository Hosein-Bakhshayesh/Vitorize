namespace Vitorize.Application.DTOs.Payments
{
    public class PaymentVerifyResultDto
    {
        public Guid PaymentId { get; set; }

        public Guid OrderId { get; set; }

        public bool IsPaid { get; set; }

        public string? ReferenceNumber { get; set; }

        public byte PaymentStatus { get; set; }

        public byte OrderStatus { get; set; }
    }
}