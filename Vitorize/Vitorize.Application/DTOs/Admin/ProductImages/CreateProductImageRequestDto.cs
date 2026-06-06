using System.ComponentModel.DataAnnotations;

namespace Vitorize.Application.DTOs.Admin.ProductImages
{
    public class CreateProductImageRequestDto
    {
        [Required]
        public string ImagePath { get; set; } = string.Empty;

        public string? AltText { get; set; }

        public int SortOrder { get; set; }

        public bool SetAsThumbnail { get; set; }
    }
}