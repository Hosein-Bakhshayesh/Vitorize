namespace Vitorize.Application.DTOs.Reviews
{
    public class ProductReviewReplyDto
    {
        public Guid Id { get; set; }

        /// <summary>عنوان ثابت و عمومی پاسخ‌دهنده؛ اطلاعات شخصی مدیر افشا نمی‌شود.</summary>
        public string AuthorLabel { get; set; } = "مدیریت";

        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
