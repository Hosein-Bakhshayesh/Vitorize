namespace Vitorize.Application.DTOs.Admin.Content
{
    public class AdminFaqDto
    {
        /// <summary>Null for the site-wide FAQ; set when the entry belongs to one product.</summary>
        public Guid? ProductId { get; set; }

        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>FAQ answers are plain text by design; no HTML field exists and none is rendered as markup.</summary>
    public class CreateFaqRequestDto
    {
        /// <summary>
        /// Scopes the entry to a product. The controller supplies it from the route for the
        /// product-owned endpoints, so a caller cannot silently re-home a global entry.
        /// </summary>
        public Guid? ProductId { get; set; }

        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateFaqRequestDto : CreateFaqRequestDto
    {
    }
}
