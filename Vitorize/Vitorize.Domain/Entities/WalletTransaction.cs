using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class WalletTransaction
{
    public Guid Id { get; set; }

    public Guid WalletId { get; set; }

    public Guid UserId { get; set; }

    public byte Type { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public byte? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Wallet Wallet { get; set; } = null!;
}
