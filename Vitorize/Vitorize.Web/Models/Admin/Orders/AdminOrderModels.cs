namespace Vitorize.Web.Models.Admin.Orders
{
    public class AdminOrderPagedResultModel
    {
        public List<AdminOrderModel> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<AdminOrderStatusCountModel> StatusCounts { get; set; } = new();
        public int StatusTotalCount { get; set; }
    }

    public class AdminOrderStatusCountModel
    {
        public byte Status { get; set; }
        public int Count { get; set; }
    }

    public class AdminOrderModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
        public byte CurrencyType { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        // Purchase-time VAT snapshot, displayed and exported as recorded on the order.
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public string? Description { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool RequiresVerification { get; set; }
        public bool VerificationCompleted { get; set; }
        public List<AdminOrderPaymentModel> Payments { get; set; } = new();
        public List<AdminOrderItemModel> Items { get; set; } = new();
        public List<AdminOrderItemModel> OrderItems { get; set; } = new();
    }

    public class AdminOrderPaymentModel
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Gateway { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? MaskedCardPan { get; set; }
        public byte Status { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }

    public class AdminOrderItemModel
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
        public List<AdminOrderInputValueModel> InputValues { get; set; } = new();
        public List<AdminOrderDeliveryModel> Deliveries { get; set; } = new();
        public AdminOrderItemKycModel? Kyc { get; set; }
    }

    public class AdminOrderDeliveryModel
    {
        public Guid Id { get; set; }
        public Guid OrderItemId { get; set; }
        public byte DeliveryType { get; set; }
        public string? DeliveredContent { get; set; }
        public bool IsVisibleToCustomer { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminOrderItemKycModel
    {
        public byte? LifecycleStatus { get; set; }
        public string? LifecycleLabel { get; set; }
        public bool BlocksFulfillment { get; set; }
        public string? PolicyTitle { get; set; }
        public Guid? PolicyVersionId { get; set; }
        public decimal EvaluatedAmount { get; set; }
        public decimal? ThresholdAmount { get; set; }
        public int? CustomerActionDeadlineHours { get; set; }
        public DateTime? CustomerActionDeadlineAt { get; set; }
        public bool IsCustomerActionOverdue { get; set; }
        public bool IsFulfilled { get; set; }
        public bool HasSupportWork { get; set; }
        public byte? FinanceResolutionStatus { get; set; }
    }

    public class AdminOrderInputValueModel
    {
        public Guid? Id { get; set; }
        public string FieldKey { get; set; } = string.Empty;
        public string FieldLabel { get; set; } = string.Empty;
        public byte FieldType { get; set; }
        public string? Value { get; set; }
        public bool IsSensitive { get; set; }
        public bool IsMasked { get; set; }
    }

    public class CancelOrderRequestModel
    {
        public string Reason { get; set; } = string.Empty;
    }
}
