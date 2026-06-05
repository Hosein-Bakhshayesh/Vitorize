using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class SecurityLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string EventType { get; set; } = null!;

    public string? Description { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool IsSuccessful { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
