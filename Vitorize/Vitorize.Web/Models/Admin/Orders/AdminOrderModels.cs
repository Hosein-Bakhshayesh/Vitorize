namespace Vitorize.Web.Models.Admin.Orders
{
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
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string? Description { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<AdminOrderItemModel> Items { get; set; } = new();
        public List<AdminOrderItemModel> OrderItems { get; set; } = new();
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
    }

    public class AdminOrderInputValueModel
    {
        public Guid? Id { get; set; }
        public string FieldKey { get; set; } = string.Empty;
        public string FieldLabel { get; set; } = string.Empty;
        public string? Value { get; set; }
        public bool IsSensitive { get; set; }
        public bool IsMasked { get; set; }
    }

    public class CancelOrderRequestModel
    {
        public string Reason { get; set; } = string.Empty;
    }
}
