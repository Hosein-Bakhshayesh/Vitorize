namespace Vitorize.Web.Models.Admin.ProductImages
{
    public class AdminProductImageModel
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public string? AltText { get; set; }

        public int SortOrder { get; set; }

        public bool IsThumbnail { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}