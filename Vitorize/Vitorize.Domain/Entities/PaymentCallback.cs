using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class PaymentCallback
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public string CallbackData { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Payment Payment { get; set; } = null!;
}
