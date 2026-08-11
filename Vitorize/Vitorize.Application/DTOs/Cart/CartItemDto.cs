namespace Vitorize.Application.DTOs.Cart
{
    public class CartItemDto
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
        public byte CurrencyType { get; set; }
        public bool RequiresKyc { get; set; }
        public byte KycRequirementMode { get; set; }
        public decimal? KycThresholdAmount { get; set; }
        public decimal KycEvaluatedAmount { get; set; }
        public Guid? KycPolicyVersionId { get; set; }
        public List<Vitorize.Application.DTOs.Products.ProductInputValueDto> InputValues { get; set; } = new();
        public List<Vitorize.Application.DTOs.Products.ProductInputFieldDto> InputFields { get; set; } = new();
    }
}
