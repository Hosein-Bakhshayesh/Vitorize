using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Faq
{
    public Guid Id { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
