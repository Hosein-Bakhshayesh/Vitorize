using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class ErrorLog
{
    public Guid Id { get; set; }

    public string Message { get; set; } = null!;

    public string? StackTrace { get; set; }

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }
}
