namespace Vitorize.Web.Models.Customer
{
    public class CustomerProfileModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string? Email { get; set; }
        public byte Status { get; set; }
        public byte VerificationStatus { get; set; }
        public bool IsMobileConfirmed { get; set; }
        public bool IsEmailConfirmed { get; set; }
    }

    public class CustomerWalletModel
    {
        public Guid WalletId { get; set; }
        public Guid UserId { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CustomerWalletTransactionModel
    {
        public Guid Id { get; set; }
        public Guid WalletId { get; set; }
        public Guid UserId { get; set; }
        public byte Type { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public byte? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerWishlistItemModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailImagePath { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountPrice { get; set; }
        public byte ProductType { get; set; }
        public byte DeliveryType { get; set; }
        public byte CurrencyType { get; set; }
        public bool RequiresVerification { get; set; }
        public string CategoryTitle { get; set; } = string.Empty;
        public string? BrandTitle { get; set; }
        public bool HasVariants { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }

        public decimal FinalPrice => DiscountPrice is > 0 && DiscountPrice < BasePrice ? DiscountPrice.Value : BasePrice;
        public bool HasDiscount => DiscountPrice is > 0 && DiscountPrice < BasePrice;
        public int DiscountPercent => HasDiscount && BasePrice > 0
            ? (int)Math.Round((BasePrice - DiscountPrice!.Value) / BasePrice * 100)
            : 0;
    }

    public class CustomerDeliveredCodeModel
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid OrderItemId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? VariantTitle { get; set; }
        public byte DeliveryType { get; set; }
        public string? DeliveredContent { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerTopUpStartModel
    {
        public Guid TopUpId { get; set; }
        public decimal Amount { get; set; }
        public string Gateway { get; set; } = string.Empty;
        public string? Authority { get; set; }
        public string? PaymentUrl { get; set; }
    }

    public class CustomerTopUpVerifyModel
    {
        public Guid TopUpId { get; set; }
        public bool IsPaid { get; set; }
        public string? ReferenceNumber { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
    }

    public class CustomerVerificationModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string NationalCode { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string? BankCardNumber { get; set; }
        public string? ShabaNumber { get; set; }
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
        public byte Status { get; set; }
        public string? AdminNote { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public List<CustomerVerificationDocumentModel> Documents { get; set; } = new();
    }

    public class CustomerVerificationDocumentModel
    {
        public Guid Id { get; set; }
        public byte DocumentType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string? AdminNote { get; set; }
    }

    public class CustomerTicketModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? OrderId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public byte Department { get; set; }
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public List<CustomerTicketMessageModel> Messages { get; set; } = new();
    }

    public class CustomerTicketMessageModel
    {
        public Guid Id { get; set; }
        public Guid TicketId { get; set; }
        public Guid SenderUserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public bool IsInternalNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerNotificationModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public byte Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class CustomerReviewModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid UserId { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Comment { get; set; } = string.Empty;
        public byte Rating { get; set; }
        public bool IsBuyer { get; set; }
        public bool IsApproved { get; set; }
        public bool IsRejected { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public byte? MyVote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CustomerReviewSummaryModel
    {
        public Guid ProductId { get; set; }
        public int TotalApprovedReviews { get; set; }
        public double AverageRating { get; set; }
        public int FiveStarCount { get; set; }
        public int FourStarCount { get; set; }
        public int ThreeStarCount { get; set; }
        public int TwoStarCount { get; set; }
        public int OneStarCount { get; set; }
    }

    public class CustomerReviewListModel
    {
        public CustomerReviewSummaryModel Summary { get; set; } = new();
        public Vitorize.Shared.Common.PagedResult<CustomerReviewModel> Reviews { get; set; } = new();
    }
}
