using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class IdempotencyKey
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string Key { get; set; } = null!;

    public string? RequestHash { get; set; }

    public string? ResponseJson { get; set; }

    public int? StatusCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public byte Status { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? FailedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public virtual User? User { get; set; }
}
