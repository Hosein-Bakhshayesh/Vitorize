using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.ProductImages
{
    public class UpdateProductImageRequestModel
    {
        [Required]
        public string ImagePath { get; set; } = string.Empty;

        public string? AltText { get; set; }

        public int SortOrder { get; set; }

        public bool IsThumbnail { get; set; }
    }
}