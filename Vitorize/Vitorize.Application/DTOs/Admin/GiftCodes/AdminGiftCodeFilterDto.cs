namespace Vitorize.Application.DTOs.Admin.GiftCodes
{
    public class AdminGiftCodeFilterDto
    {
        public Guid? ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public Guid? BatchId { get; set; }

        /// <summary>
        /// وضعیت گیفت‌کد: 0 موجود، 1 رزرو، 2 فروخته‌شده، 3 تحویل‌شده، 4 منقضی، 5 غیرفعال.
        /// </summary>
        public byte? Status { get; set; }

        /// <summary>
        /// جست‌وجو روی کد ماسک‌شده یا شماره سریال.
        /// </summary>
        public string? Search { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
