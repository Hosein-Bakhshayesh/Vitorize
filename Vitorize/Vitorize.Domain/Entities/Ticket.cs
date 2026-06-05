using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Ticket
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? OrderId { get; set; }

    public string Subject { get; set; } = null!;

    public byte Department { get; set; }

    public byte Priority { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();

    public virtual User User { get; set; } = null!;
}
