using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.GiftCodes
{
    public class GiftCodeBatchModel
    {
        public Guid Id { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? ProductVariantTitle { get; set; }
        public string BatchTitle { get; set; } = string.Empty;
        public string? SourceName { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? Notes { get; set; }
        public DateTime ImportedAt { get; set; }
        public int TotalCodes { get; set; }
        public int AvailableCodes { get; set; }
        public int SoldCodes { get; set; }
        public int DisabledCodes { get; set; }
    }

    public class GiftCodeImportRequestModel
    {
        [Required] public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        [Required, MaxLength(200)] public string BatchTitle { get; set; } = string.Empty;
        [MaxLength(200)] public string? SourceName { get; set; }
        public decimal? PurchasePrice { get; set; }
        [MaxLength(1000)] public string? Notes { get; set; }
        public List<string> Codes { get; set; } = new();
    }
}
