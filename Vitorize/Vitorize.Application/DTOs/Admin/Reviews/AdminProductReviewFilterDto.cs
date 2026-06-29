namespace Vitorize.Application.DTOs.Admin.Reviews
{
    public class AdminProductReviewFilterDto
    {
        public Guid? ProductId { get; set; }

        public Guid? UserId { get; set; }

        public byte? Rating { get; set; }

        /// <summary>
        /// فیلتر وضعیت: null همه، true فقط تأییدشده، false فقط تأییدنشده.
        /// </summary>
        public bool? IsApproved { get; set; }

        public bool? IsRejected { get; set; }

        public string? Search { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
