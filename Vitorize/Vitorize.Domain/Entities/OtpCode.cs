using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class OtpCode
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string CodeHash { get; set; } = null!;

    public byte Purpose { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
