namespace Vitorize.Application.DTOs.Admin.Content
{
    public class AdminFaqDto
    {
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
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateFaqRequestDto : CreateFaqRequestDto
    {
    }
}
