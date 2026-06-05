using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public string Gateway { get; set; } = null!;

    public string? Authority { get; set; }

    public string? GatewayTrackingCode { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? TransactionId { get; set; }

    public string? ReferenceNumber { get; set; }

    public byte Status { get; set; }

    public string? ProviderStatusCode { get; set; }

    public bool CallbackVerified { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? RawRequestData { get; set; }

    public string? RawResponseData { get; set; }

    public string? ErrorMessage { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<PaymentCallback> PaymentCallbacks { get; set; } = new List<PaymentCallback>();

    public virtual User User { get; set; } = null!;
}
