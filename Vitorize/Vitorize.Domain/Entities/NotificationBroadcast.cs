using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

/// <summary>
/// FIX-15: the header record for one admin group announcement. Delivery itself remains ordinary
/// per-user <see cref="Notification"/> rows; this row exists for history, idempotency and audit.
/// A sent broadcast is immutable.
/// </summary>
public partial class NotificationBroadcast
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    /// <summary>Persisted <c>BroadcastAudience</c>: 1 = AllCustomers, 2 = SelectedCustomers.</summary>
    public byte AudienceType { get; set; }

    /// <summary>Number of notification rows actually delivered, never the preview estimate.</summary>
    public int RecipientCount { get; set; }

    /// <summary>Persisted <c>BroadcastStatus</c>: 1 = Sending, 2 = Sent, 3 = Failed.</summary>
    public byte Status { get; set; }

    /// <summary>Optional internal, relative call-to-action path.</summary>
    public string? ActionUrl { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
