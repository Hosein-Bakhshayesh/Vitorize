namespace Vitorize.Application.DTOs.Admin.GiftCodes
{
    public class GiftCodeImportDto
    {
        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public string BatchTitle { get; set; } = string.Empty;

        public string? SourceName { get; set; }

        public decimal? PurchasePrice { get; set; }

        public string? Notes { get; set; }

        public List<string> Codes { get; set; } = new();
    }
}