namespace Vitorize.Web.Models.Storefront
{
    public class StoreAddToCartRequestModel
    {
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class StoreUpdateCartItemRequestModel
    {
        public int Quantity { get; set; }
    }

    public class StoreCartModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public List<StoreCartItemModel> Items { get; set; } = new();
        public int TotalQuantity { get; set; }
        public decimal SubtotalAmount { get; set; }
    }

    public class StoreCartItemModel
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
    }
}
