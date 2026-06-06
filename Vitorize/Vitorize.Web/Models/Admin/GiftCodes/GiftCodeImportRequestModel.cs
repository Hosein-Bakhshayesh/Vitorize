using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.GiftCodes
{
    public class GiftCodeImportRequestModel
    {
        [Required(ErrorMessage = "محصول الزامی است.")]
        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        [Required(ErrorMessage = "عنوان بچ الزامی است.")]
        [MaxLength(200)]
        public string BatchTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? SourceName { get; set; }

        public decimal? PurchasePrice { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public List<string> Codes { get; set; } = new();
    }
}