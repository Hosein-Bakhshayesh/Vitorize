namespace Vitorize.Application.DTOs.Admin.Payments
{
    public class AdminPaymentDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Gateway { get; set; } = string.Empty;
        public string? Authority { get; set; }
        public string? GatewayTrackingCode { get; set; }
        public string? TransactionId { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? MaskedCardPan { get; set; }
        public byte Status { get; set; }
        public string? ProviderStatusCode { get; set; }
        public bool CallbackVerified { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public List<Vitorize.Application.DTOs.Payments.PaymentRefundDto> Refunds { get; set; } = [];
        public List<FinancialAuditEntryDto> AuditHistory { get; set; } = [];
    }

    public sealed class FinancialAuditEntryDto
    {
        public string EventType { get; set; } = string.Empty;
        public Guid? EntityId { get; set; }
        public decimal? Amount { get; set; }
        public string? Detail { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
