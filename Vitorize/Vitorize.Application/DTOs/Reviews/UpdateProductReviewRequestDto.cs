namespace Vitorize.Application.DTOs.Reviews
{
    public class UpdateProductReviewRequestDto
    {
        public string? Title { get; set; }

        public string Comment { get; set; } = string.Empty;

        public byte Rating { get; set; }
    }
}
