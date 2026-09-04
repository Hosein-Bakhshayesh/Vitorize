namespace Vitorize.Application.DTOs.Admin.Reviews
{
    public class AdminProductReviewReplyDto
    {
        public Guid Id { get; set; }

        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
