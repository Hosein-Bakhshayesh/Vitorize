namespace Vitorize.Application.DTOs.Reviews
{
    public class ProductReviewDto
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public Guid UserId { get; set; }

        public string UserDisplayName { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string Comment { get; set; } = string.Empty;

        public byte Rating { get; set; }

        public bool IsBuyer { get; set; }

        public bool IsApproved { get; set; }

        public bool IsRejected { get; set; }

        public int LikeCount { get; set; }

        public int DislikeCount { get; set; }

        /// <summary>
        /// نوع رأی کاربر فعلی روی این نظر (در صورت احراز هویت): 1 مفید، 2 غیرمفید، null بدون رأی.
        /// </summary>
        public byte? MyVote { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
