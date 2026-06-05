using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class TicketMessage
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Guid SenderUserId { get; set; }

    public string Message { get; set; } = null!;

    public string? AttachmentPath { get; set; }

    public bool IsInternalNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User SenderUser { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;
}
