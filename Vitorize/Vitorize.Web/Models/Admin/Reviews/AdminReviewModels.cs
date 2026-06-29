using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Reviews
{
    public class AdminProductReviewModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string? UserMobile { get; set; }
        public string? Title { get; set; }
        public string Comment { get; set; } = string.Empty;
        public byte Rating { get; set; }
        public bool IsApproved { get; set; }
        public bool IsRejected { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsBuyer { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class RejectReviewRequestModel
    {
        [Required(ErrorMessage = "برای رد نظر، ذکر دلیل الزامی است.")]
        [MaxLength(1000, ErrorMessage = "دلیل رد نظر نباید بیشتر از ۱۰۰۰ کاراکتر باشد.")]
        public string Reason { get; set; } = string.Empty;
    }
}
