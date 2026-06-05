using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class UserRefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public string? JwtId { get; set; }

    public string? DeviceId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
