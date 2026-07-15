using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class OutboxMessage
{
    public Guid Id { get; set; }

    public Guid? AggregateId { get; set; }

    public string? AggregateType { get; set; }

    public string MessageType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public byte Status { get; set; }

    public int RetryCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime? LockedAt { get; set; }

    public Guid? LockId { get; set; }
}
