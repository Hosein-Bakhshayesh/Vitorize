using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class ProductReviewVote
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }

    public Guid UserId { get; set; }

    public byte VoteType { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ProductReview Review { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
