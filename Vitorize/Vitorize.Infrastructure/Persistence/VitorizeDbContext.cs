using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;

namespace Vitorize.Infrastructure.Persistence;

public partial class VitorizeDbContext : DbContext
{
    public VitorizeDbContext()
    {
    }

    public VitorizeDbContext(DbContextOptions<VitorizeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<BlogPost> BlogPosts { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<CouponUsage> CouponUsages { get; set; }

    public virtual DbSet<ErrorLog> ErrorLogs { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<GiftCode> GiftCodes { get; set; }

    public virtual DbSet<GiftCodeBatch> GiftCodeBatches { get; set; }

    public virtual DbSet<GiftCodeReservation> GiftCodeReservations { get; set; }

    public virtual DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderItemDelivery> OrderItemDeliveries { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<OtpCode> OtpCodes { get; set; }

    public virtual DbSet<OutboxMessage> OutboxMessages { get; set; }

    public virtual DbSet<Page> Pages { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentCallback> PaymentCallbacks { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<ProductReviewVote> ProductReviewVotes { get; set; }

    public virtual DbSet<ProductTag> ProductTags { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SecurityLog> SecurityLogs { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketMessage> TicketMessages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAddress> UserAddresses { get; set; }

    public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

    public virtual DbSet<UserVerificationProfile> UserVerificationProfiles { get; set; }

    public virtual DbSet<VerificationDocument> VerificationDocuments { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    public virtual DbSet<WishList> WishLists { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.;Database=VitorizeDb;Trusted_Connection=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => new { e.EntityName, e.EntityId }, "IX_AuditLogs_Entity");

            entity.HasIndex(e => e.UserId, "IX_AuditLogs_UserId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ActionType).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_Users");
        });

        modelBuilder.Entity<Banner>(entity =>
        {
            entity.HasIndex(e => e.Position, "IX_Banners_Position");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.MobileImagePath).HasMaxLength(500);
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UX_BlogPosts_Slug").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CoverImagePath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SeoDescription).HasMaxLength(500);
            entity.Property(e => e.SeoTitle).HasMaxLength(250);
            entity.Property(e => e.Slug).HasMaxLength(300);
            entity.Property(e => e.Summary).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(300);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UX_Brands_Slug").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Slug).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(150);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UX_Carts_UserId").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Carts_Users");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(e => e.CartId, "IX_CartItems_CartId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItems_Carts");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItems_Products");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("FK_CartItems_ProductVariants");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.ParentId, "IX_Categories_ParentId");

            entity.HasIndex(e => e.Slug, "UX_Categories_Slug").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SeoDescription).HasMaxLength(500);
            entity.Property(e => e.SeoTitle).HasMaxLength(250);
            entity.Property(e => e.Slug).HasMaxLength(250);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK_Categories_Parent");
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(e => e.Code, "UX_Coupons_Code").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Code).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.HasIndex(e => e.CouponId, "IX_CouponUsages_CouponId");

            entity.HasIndex(e => e.UserId, "IX_CouponUsages_UserId");

            entity.HasIndex(e => e.OrderId, "UX_CouponUsages_OrderId").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.UsedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Coupon).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CouponUsages_Coupons");

            entity.HasOne(d => d.Order).WithOne(p => p.CouponUsage)
                .HasForeignKey<CouponUsage>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CouponUsages_Orders");

            entity.HasOne(d => d.User).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CouponUsages_Users");
        });

        modelBuilder.Entity<ErrorLog>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Source).HasMaxLength(300);
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.ToTable("FAQs");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Question).HasMaxLength(500);
        });

        modelBuilder.Entity<GiftCode>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.ProductVariantId, e.Status, e.CreatedAt }, "IX_GiftCodes_Available_ProductVariant").HasFilter("([Status]=(0))");

            entity.HasIndex(e => e.OrderItemId, "IX_GiftCodes_OrderItemId");

            entity.HasIndex(e => new { e.ProductVariantId, e.Status }, "IX_GiftCodes_ProductVariant_Status");

            entity.HasIndex(e => new { e.ProductId, e.Status }, "IX_GiftCodes_Product_Status");

            entity.HasIndex(e => new { e.Status, e.ReservationExpiresAt }, "IX_GiftCodes_ReservationExpiresAt").HasFilter("([Status]=(1))");

            entity.HasIndex(e => e.Status, "IX_GiftCodes_Status");

            entity.HasIndex(e => e.CodeHashFingerprint, "UX_GiftCodes_CodeHashFingerprint")
                .IsUnique()
                .HasFilter("([CodeHashFingerprint] IS NOT NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CodeHashFingerprint).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EncryptionVersion).HasDefaultValue(1);
            entity.Property(e => e.ExtraData).HasMaxLength(1000);
            entity.Property(e => e.MaskedCode).HasMaxLength(100);
            entity.Property(e => e.SerialNumber).HasMaxLength(200);

            entity.HasOne(d => d.Batch).WithMany(p => p.GiftCodes)
                .HasForeignKey(d => d.BatchId)
                .HasConstraintName("FK_GiftCodes_GiftCodeBatches");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.GiftCodes)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_GiftCodes_OrderItems");

            entity.HasOne(d => d.Product).WithMany(p => p.GiftCodes)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiftCodes_Products");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.GiftCodes)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("FK_GiftCodes_ProductVariants");

            entity.HasOne(d => d.ReservedByUser).WithMany(p => p.GiftCodes)
                .HasForeignKey(d => d.ReservedByUserId)
                .HasConstraintName("FK_GiftCodes_ReservedByUser");
        });

        modelBuilder.Entity<GiftCodeBatch>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_GiftCodeBatches_ProductId");

            entity.HasIndex(e => e.ProductVariantId, "IX_GiftCodeBatches_ProductVariantId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.BatchTitle).HasMaxLength(200);
            entity.Property(e => e.ImportedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SourceName).HasMaxLength(200);

            entity.HasOne(d => d.ImportedByAdmin).WithMany(p => p.GiftCodeBatches)
                .HasForeignKey(d => d.ImportedByAdminId)
                .HasConstraintName("FK_GiftCodeBatches_ImportedByAdmin");

            entity.HasOne(d => d.Product).WithMany(p => p.GiftCodeBatches)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_GiftCodeBatches_Products");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.GiftCodeBatches)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("FK_GiftCodeBatches_ProductVariants");
        });

        modelBuilder.Entity<GiftCodeReservation>(entity =>
        {
            entity.HasIndex(e => e.OrderItemId, "IX_GiftCodeReservations_OrderItemId").HasFilter("([OrderItemId] IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.Status, e.ReservedAt }, "IX_GiftCodeReservations_UserId_Status");

            entity.HasIndex(e => e.GiftCodeId, "UX_GiftCodeReservations_Active_GiftCode")
                .IsUnique()
                .HasFilter("([Status]=(1))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ReservedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status).HasDefaultValue((byte)1);

            entity.HasOne(d => d.GiftCode).WithOne(p => p.GiftCodeReservation)
                .HasForeignKey<GiftCodeReservation>(d => d.GiftCodeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiftCodeReservations_GiftCodes");

            entity.HasOne(d => d.Order).WithMany(p => p.GiftCodeReservations)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_GiftCodeReservations_Orders");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.GiftCodeReservations)
                .HasForeignKey(d => d.OrderItemId)
                .HasConstraintName("FK_GiftCodeReservations_OrderItems");

            entity.HasOne(d => d.Product).WithMany(p => p.GiftCodeReservations)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiftCodeReservations_Products");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.GiftCodeReservations)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("FK_GiftCodeReservations_ProductVariants");

            entity.HasOne(d => d.User).WithMany(p => p.GiftCodeReservations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiftCodeReservations_Users");
        });

        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_IdempotencyKeys_UserId_CreatedAt");

            entity.HasIndex(e => e.Key, "UX_IdempotencyKeys_Key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.Key).HasMaxLength(200);
            entity.Property(e => e.RequestHash).HasMaxLength(500);
            entity.Property(e => e.Status).HasDefaultValue((byte)1);

            entity.HasOne(d => d.User).WithMany(p => p.IdempotencyKeys)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_IdempotencyKeys_Users");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_Notifications_UserId_IsRead");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(250);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.Status, "IX_Orders_Status");

            entity.HasIndex(e => e.UserId, "IX_Orders_UserId");

            entity.HasIndex(e => e.OrderNumber, "UX_Orders_OrderNumber").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AdminNote).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.SubtotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Coupon).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CouponId)
                .HasConstraintName("FK_Orders_Coupons");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Users");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(e => e.OrderId, "IX_OrderItems_OrderId");

            entity.HasIndex(e => e.ProductId, "IX_OrderItems_ProductId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ProductTitle).HasMaxLength(250);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VariantTitle).HasMaxLength(200);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_Orders");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_Products");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductVariantId)
                .HasConstraintName("FK_OrderItems_ProductVariants");

            entity.HasOne(d => d.SupportTicket).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.SupportTicketId)
                .HasConstraintName("FK_OrderItems_Tickets");
        });

        modelBuilder.Entity<OrderItemDelivery>(entity =>
        {
            entity.HasIndex(e => e.GiftCodeId, "IX_OrderItemDeliveries_GiftCodeId");

            entity.HasIndex(e => e.OrderItemId, "IX_OrderItemDeliveries_OrderItemId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsVisibleToCustomer).HasDefaultValue(true);

            entity.HasOne(d => d.DeliveredByUser).WithMany(p => p.OrderItemDeliveries)
                .HasForeignKey(d => d.DeliveredByUserId)
                .HasConstraintName("FK_OrderItemDeliveries_DeliveredByUser");

            entity.HasOne(d => d.GiftCode).WithMany(p => p.OrderItemDeliveries)
                .HasForeignKey(d => d.GiftCodeId)
                .HasConstraintName("FK_OrderItemDeliveries_GiftCodes");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.OrderItemDeliveries)
                .HasForeignKey(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItemDeliveries_OrderItems");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasIndex(e => e.OrderId, "IX_OrderStatusHistories_OrderId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Note).HasMaxLength(1000);

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_OrderStatusHistories_ChangedByUser");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderStatusHistories_Orders");
        });

        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.HasIndex(e => new { e.Email, e.Purpose, e.ExpiresAt }, "IX_OtpCodes_Email_Purpose_ExpiresAt").HasFilter("([Email] IS NOT NULL AND [ConsumedAt] IS NULL)");

            entity.HasIndex(e => new { e.Mobile, e.Purpose, e.ExpiresAt }, "IX_OtpCodes_Mobile_Purpose_ExpiresAt").HasFilter("([Mobile] IS NOT NULL AND [ConsumedAt] IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CodeHash).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.MaxAttempt).HasDefaultValue(5);
            entity.Property(e => e.Mobile).HasMaxLength(20);
            entity.Property(e => e.UserAgent).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithMany(p => p.OtpCodes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_OtpCodes_Users");
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_OutboxMessages_Status_CreatedAt");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AggregateType).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.MessageType).HasMaxLength(300);
        });

        modelBuilder.Entity<Page>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UX_Pages_Slug").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsPublished).HasDefaultValue(true);
            entity.Property(e => e.SeoDescription).HasMaxLength(500);
            entity.Property(e => e.SeoTitle).HasMaxLength(250);
            entity.Property(e => e.Slug).HasMaxLength(250);
            entity.Property(e => e.Title).HasMaxLength(250);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.Authority, "IX_Payments_Authority").HasFilter("([Authority] IS NOT NULL)");

            entity.HasIndex(e => e.OrderId, "IX_Payments_OrderId");

            entity.HasIndex(e => new { e.OrderId, e.Status, e.RequestedAt }, "IX_Payments_OrderId_Status");

            entity.HasIndex(e => e.TransactionId, "IX_Payments_TransactionId");

            entity.HasIndex(e => e.UserId, "IX_Payments_UserId");

            entity.HasIndex(e => e.IdempotencyKey, "UX_Payments_IdempotencyKey")
                .IsUnique()
                .HasFilter("([IdempotencyKey] IS NOT NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Authority).HasMaxLength(300);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.Gateway).HasMaxLength(100);
            entity.Property(e => e.GatewayTrackingCode).HasMaxLength(300);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.ProviderStatusCode).HasMaxLength(100);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.TransactionId).HasMaxLength(200);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Orders");

            entity.HasOne(d => d.User).WithMany(p => p.Payments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payments_Users");
        });

        modelBuilder.Entity<PaymentCallback>(entity =>
        {
            entity.HasIndex(e => e.PaymentId, "IX_PaymentCallbacks_PaymentId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Payment).WithMany(p => p.PaymentCallbacks)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentCallbacks_Payments");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.BrandId, "IX_Products_BrandId");

            entity.HasIndex(e => e.CategoryId, "IX_Products_CategoryId");

            entity.HasIndex(e => e.Slug, "UX_Products_Slug").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiscountPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinOrderQuantity).HasDefaultValue(1);
            entity.Property(e => e.SeoDescription).HasMaxLength(500);
            entity.Property(e => e.SeoTitle).HasMaxLength(250);
            entity.Property(e => e.ShortDescription).HasMaxLength(1000);
            entity.Property(e => e.Slug).HasMaxLength(300);
            entity.Property(e => e.ThumbnailImagePath).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(250);

            entity.HasOne(d => d.Brand).WithMany(p => p.Products)
                .HasForeignKey(d => d.BrandId)
                .HasConstraintName("FK_Products_Brands");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Categories");

            entity.HasMany(d => d.Tags).WithMany(p => p.Products)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductTagMapping",
                    r => r.HasOne<ProductTag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductTagMappings_ProductTags"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductTagMappings_Products"),
                    j =>
                    {
                        j.HasKey("ProductId", "TagId");
                        j.ToTable("ProductTagMappings");
                    });
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_ProductImages_ProductId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AltText).HasMaxLength(250);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ImagePath).HasMaxLength(500);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImages_Products");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductR__3214EC07AF057B43");

            entity.HasIndex(e => e.IsApproved, "IX_ProductReviews_IsApproved");

            entity.HasIndex(e => e.ParentId, "IX_ProductReviews_ParentId");

            entity.HasIndex(e => e.ProductId, "IX_ProductReviews_ProductId");

            entity.HasIndex(e => e.UserId, "IX_ProductReviews_UserId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK_ProductReviews_Parent");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReviews_Product");

            entity.HasOne(d => d.User).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReviews_User");
        });

        modelBuilder.Entity<ProductReviewVote>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductR__3214EC075731752C");

            entity.HasIndex(e => new { e.ReviewId, e.UserId }, "UQ_ProductReviewVotes_Review_User").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Review).WithMany(p => p.ProductReviewVotes)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReviewVotes_Review");

            entity.HasOne(d => d.User).WithMany(p => p.ProductReviewVotes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReviewVotes_User");
        });

        modelBuilder.Entity<ProductTag>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UX_ProductTags_Slug").IsUnique();

            entity.HasIndex(e => e.Title, "UX_ProductTags_Title").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Slug).HasMaxLength(150);
            entity.Property(e => e.Title).HasMaxLength(100);
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_ProductVariants_ProductId");

            entity.HasIndex(e => e.Sku, "UX_ProductVariants_Sku")
                .IsUnique()
                .HasFilter("([Sku] IS NOT NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiscountPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Sku).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Value).HasMaxLength(100);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVariants_Products");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name, "UX_Roles_Name").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisplayName).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<SecurityLog>(entity =>
        {
            entity.HasIndex(e => new { e.EventType, e.CreatedAt }, "IX_SecurityLogs_EventType_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_SecurityLogs_UserId_CreatedAt").IsDescending(false, true);

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EventType).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithMany(p => p.SecurityLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_SecurityLogs_Users");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasIndex(e => e.Key, "UX_Settings_Key").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.GroupName).HasMaxLength(100);
            entity.Property(e => e.Key).HasMaxLength(200);
            entity.Property(e => e.ValueType).HasMaxLength(50);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(e => e.OrderId, "IX_Tickets_OrderId");

            entity.HasIndex(e => e.UserId, "IX_Tickets_UserId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Priority).HasDefaultValue((byte)1);
            entity.Property(e => e.Subject).HasMaxLength(250);

            entity.HasOne(d => d.Order).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_Tickets_Orders");

            entity.HasOne(d => d.User).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tickets_Users");
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasIndex(e => e.TicketId, "IX_TicketMessages_TicketId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AttachmentPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.TicketMessages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TicketMessages_Users");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketMessages)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TicketMessages_Tickets");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UX_Users_Email")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.HasIndex(e => e.Mobile, "UX_Users_Mobile").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.Mobile).HasMaxLength(20);
            entity.Property(e => e.NationalCode).HasMaxLength(20);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserRoles_Roles"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserRoles_Users"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("UserRoles");
                    });
        });

        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_UserAddresses_UserId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AddressLine).HasMaxLength(1000);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.Province).HasMaxLength(100);
            entity.Property(e => e.ReceiverName).HasMaxLength(150);
            entity.Property(e => e.Title).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.UserAddresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAddresses_Users");
        });

        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt }, "IX_UserRefreshTokens_UserId_ExpiresAt");

            entity.HasIndex(e => e.TokenHash, "UX_UserRefreshTokens_TokenHash").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DeviceId).HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.JwtId).HasMaxLength(200);
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(500);
            entity.Property(e => e.RevocationReason).HasMaxLength(500);
            entity.Property(e => e.TokenHash).HasMaxLength(500);
            entity.Property(e => e.UserAgent).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithMany(p => p.UserRefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRefreshTokens_Users");
        });

        modelBuilder.Entity<UserVerificationProfile>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UX_UserVerificationProfiles_UserId").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Address).HasMaxLength(1000);
            entity.Property(e => e.AdminNote).HasMaxLength(1000);
            entity.Property(e => e.BankCardNumber).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.NationalCode).HasMaxLength(20);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.ShabaNumber).HasMaxLength(50);

            entity.HasOne(d => d.ReviewedByAdmin).WithMany(p => p.UserVerificationProfileReviewedByAdmins)
                .HasForeignKey(d => d.ReviewedByAdminId)
                .HasConstraintName("FK_UserVerificationProfiles_ReviewedByAdmin");

            entity.HasOne(d => d.User).WithOne(p => p.UserVerificationProfileUser)
                .HasForeignKey<UserVerificationProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserVerificationProfiles_Users");
        });

        modelBuilder.Entity<VerificationDocument>(entity =>
        {
            entity.HasIndex(e => e.UserVerificationProfileId, "IX_VerificationDocuments_ProfileId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AdminNote).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FilePath).HasMaxLength(500);

            entity.HasOne(d => d.ReviewedByAdmin).WithMany(p => p.VerificationDocuments)
                .HasForeignKey(d => d.ReviewedByAdminId)
                .HasConstraintName("FK_VerificationDocuments_ReviewedByAdmin");

            entity.HasOne(d => d.UserVerificationProfile).WithMany(p => p.VerificationDocuments)
                .HasForeignKey(d => d.UserVerificationProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VerificationDocuments_UserVerificationProfiles");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UX_Wallets_UserId").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Balance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Wallets_Users");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_WalletTransactions_UserId");

            entity.HasIndex(e => e.WalletId, "IX_WalletTransactions_WalletId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WalletTransactions_Users");

            entity.HasOne(d => d.Wallet).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.WalletId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WalletTransactions_Wallets");
        });

        modelBuilder.Entity<WishList>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WishList__3214EC076FBD5F8D");

            entity.ToTable("WishList");

            entity.HasIndex(e => e.ProductId, "IX_WishList_ProductId");

            entity.HasIndex(e => e.UserId, "IX_WishList_UserId");

            entity.HasIndex(e => new { e.UserId, e.ProductId }, "UQ_WishList_User_Product").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Product).WithMany(p => p.WishLists)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WishList_Product");

            entity.HasOne(d => d.User).WithMany(p => p.WishLists)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WishList_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
