using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class ProductReview
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ParentId { get; set; }

    public string? Title { get; set; }

    public string Comment { get; set; } = null!;

    public byte Rating { get; set; }

    public bool IsApproved { get; set; }

    public bool IsRejected { get; set; }

    public string? RejectionReason { get; set; }

    public bool IsBuyer { get; set; }

    public int LikeCount { get; set; }

    public int DislikeCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<ProductReview> InverseParent { get; set; } = new List<ProductReview>();

    public virtual ProductReview? Parent { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductReviewVote> ProductReviewVotes { get; set; } = new List<ProductReviewVote>();

    public virtual User User { get; set; } = null!;
}
