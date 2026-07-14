namespace Vitorize.Web.Models.Store
{
    public class CartModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public List<CartItemModel> Items { get; set; } = new();
        public int TotalQuantity { get; set; }
        public decimal SubtotalAmount { get; set; }
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

    public class StoreOrderModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
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
    }
}
