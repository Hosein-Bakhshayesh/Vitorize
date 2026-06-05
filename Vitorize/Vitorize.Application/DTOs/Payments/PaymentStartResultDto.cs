namespace Vitorize.Application.DTOs.Payments
{
    public class PaymentStartResultDto
    {
        public Guid PaymentId { get; set; }

        public Guid OrderId { get; set; }

        public decimal Amount { get; set; }

        public string Gateway { get; set; } = string.Empty;

        public string? Authority { get; set; }

        public string PaymentUrl { get; set; } = string.Empty;
    }
}