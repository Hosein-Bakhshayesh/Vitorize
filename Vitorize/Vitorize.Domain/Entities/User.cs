using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Mobile { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string? NationalCode { get; set; }

    public string? AvatarPath { get; set; }

    public byte Status { get; set; }

    public byte VerificationStatus { get; set; }

    public bool IsMobileConfirmed { get; set; }

    public bool IsEmailConfirmed { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual Cart? Cart { get; set; }

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual ICollection<GiftCodeBatch> GiftCodeBatches { get; set; } = new List<GiftCodeBatch>();

    public virtual ICollection<GiftCodeReservation> GiftCodeReservations { get; set; } = new List<GiftCodeReservation>();

    public virtual ICollection<GiftCode> GiftCodes { get; set; } = new List<GiftCode>();

    public virtual ICollection<IdempotencyKey> IdempotencyKeys { get; set; } = new List<IdempotencyKey>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OrderItemDelivery> OrderItemDeliveries { get; set; } = new List<OrderItemDelivery>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<ProductReviewVote> ProductReviewVotes { get; set; } = new List<ProductReviewVote>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<SecurityLog> SecurityLogs { get; set; } = new List<SecurityLog>();

    public virtual ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public virtual ICollection<UserAddress> UserAddresses { get; set; } = new List<UserAddress>();

    public virtual ICollection<UserRefreshToken> UserRefreshTokens { get; set; } = new List<UserRefreshToken>();

    public virtual ICollection<UserVerificationProfile> UserVerificationProfileReviewedByAdmins { get; set; } = new List<UserVerificationProfile>();

    public virtual UserVerificationProfile? UserVerificationProfileUser { get; set; }

    public virtual ICollection<VerificationDocument> VerificationDocuments { get; set; } = new List<VerificationDocument>();

    public virtual Wallet? Wallet { get; set; }

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

    public virtual ICollection<WalletTopUp> WalletTopUps { get; set; } = new List<WalletTopUp>();

    public virtual ICollection<WishList> WishLists { get; set; } = new List<WishList>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
