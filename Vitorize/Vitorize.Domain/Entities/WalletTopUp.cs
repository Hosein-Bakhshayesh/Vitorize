using System;

namespace Vitorize.Domain.Entities;

public partial class WalletTopUp
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public string Gateway { get; set; } = null!;

    public string? Authority { get; set; }

    public string? ReferenceNumber { get; set; }

    public byte Status { get; set; }

    public string? ErrorMessage { get; set; }

    public string? RawResponseData { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
