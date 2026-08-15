namespace Vitorize.Web.Models.Store
{
    public class CartModel
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public List<CartItemModel> Items { get; set; } = new();
        public int TotalQuantity { get; set; }
        public decimal SubtotalAmount { get; set; }
        // Server-computed. The storefront renders these and never does its own money arithmetic.
        public decimal DiscountAmount { get; set; }
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }

    public class CartItemModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? VariantTitle { get; set; }
        public string? ThumbnailImagePath { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public bool RequiresKyc { get; set; }
        public byte KycRequirementMode { get; set; }
        public decimal? KycThresholdAmount { get; set; }
        public decimal KycEvaluatedAmount { get; set; }
        public Guid? KycPolicyVersionId { get; set; }
        public List<StoreProductInputValueModel> InputValues { get; set; } = new();
        public List<StoreProductInputFieldModel> InputFields { get; set; } = new();
    }

    public class CheckoutResultModel
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public byte OrderStatus { get; set; }
        public byte PaymentStatus { get; set; }
        public List<Guid> ReservationIds { get; set; } = new();
    }

    public class ValidateCouponResultModel
    {
        // پاسخ API فیلد IsValid ندارد؛ اعتبار کوپن یعنی پاسخ موفق همراه CouponId.
        public Guid? CouponId { get; set; }
        public string? Code { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        /// <summary>Authoritative VAT-inclusive payable. Rendered as-is; never recomputed in Razor.</summary>
        public decimal FinalAmount { get; set; }
    }

    public class PaymentStartResultModel
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Gateway { get; set; } = string.Empty;
        public string? Authority { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
    }

    public class PaymentVerifyResultModel
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public bool IsPaid { get; set; }
        public string? ReferenceNumber { get; set; }
        public byte PaymentStatus { get; set; }
        public byte OrderStatus { get; set; }
    }

    public class PaymentRetryEligibilityModel
    {
        public Guid OrderId { get; set; }
        public bool CanRetry { get; set; }
        public string? Reason { get; set; }
    }

    public class StoreOrderModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        // Purchase-time snapshot. Pre-FIX-13 orders report VatEnabled = false and show no VAT row.
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<StoreOrderItemModel> Items { get; set; } = new();
    }

    public class StoreOrderItemModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? VariantTitle { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public byte DeliveryType { get; set; }
        public byte DeliveryStatus { get; set; }
        public bool RequiresVerification { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public List<StoreOrderDeliveryModel> Deliveries { get; set; } = new();
        public List<StoreProductInputValueModel> InputValues { get; set; } = new();
        public StoreOrderItemKycModel? Kyc { get; set; }
    }

    public class StoreOrderItemKycModel
    {
        public byte? LifecycleStatus { get; set; }
        public string? LifecycleLabel { get; set; }
        public bool BlocksFulfillment { get; set; }
        public string CustomerAction { get; set; } = "None";
        public string CustomerActionLabel { get; set; } = string.Empty;
        public Guid? PolicyVersionId { get; set; }
        public string? PolicyTitle { get; set; }
        public string? PolicyInstructions { get; set; }
        public decimal EvaluatedAmount { get; set; }
        public decimal? ThresholdAmount { get; set; }
        public int? CustomerActionDeadlineHours { get; set; }
        public DateTime? CustomerActionDeadlineAt { get; set; }
        public bool IsCustomerActionOverdue { get; set; }
        public bool IsFulfilled { get; set; }
        public bool HasSupportWork { get; set; }
        public byte? FinanceResolutionStatus { get; set; }
        public List<StoreOrderItemKycDocumentModel> Documents { get; set; } = new();
    }

    public class StoreOrderItemKycDocumentModel
    {
        public Guid DocumentTypeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public bool IsRequired { get; set; }
        public byte RedactionMode { get; set; }
        public string? RedactionInstructions { get; set; }
        public string UploadStatus { get; set; } = string.Empty;
    }

    public class StoreProductInputValueModel
    {
        public Guid? Id { get; set; }
        public Guid? ProductInputFieldId { get; set; }
        public string FieldKey { get; set; } = string.Empty;
        public string FieldLabel { get; set; } = string.Empty;
        public byte FieldType { get; set; }
        public string? Value { get; set; }
        public bool IsSensitive { get; set; }
        public bool IsMasked { get; set; }
    }

    public class StoreOrderDeliveryModel
    {
        public Guid Id { get; set; }
        public Guid OrderItemId { get; set; }
        public byte DeliveryType { get; set; }
        public Guid? GiftCodeId { get; set; }
        public string? DeliveredContent { get; set; }
        public bool IsVisibleToCustomer { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StorePageModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ContentHtml { get; set; } = string.Empty;
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
