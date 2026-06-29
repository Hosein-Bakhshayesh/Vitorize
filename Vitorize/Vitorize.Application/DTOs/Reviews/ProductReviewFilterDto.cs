namespace Vitorize.Application.DTOs.Reviews
{
    public class ProductReviewFilterDto
    {
        public Guid ProductId { get; set; }

        /// <summary>
        /// مرتب‌سازی: newest (پیش‌فرض)، oldest، highest، lowest، helpful.
        /// </summary>
        public string? Sort { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;
    }
}
