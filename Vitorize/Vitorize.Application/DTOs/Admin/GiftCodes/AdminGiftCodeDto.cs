namespace Vitorize.Application.DTOs.Admin.GiftCodes
{
    /// <summary>
    /// نمای امن یک گیفت‌کد برای پنل ادمین؛ هرگز شامل مقدار رمزگذاری‌شده/خام کد نیست.
    /// </summary>
    public class AdminGiftCodeDto
    {
        public Guid Id { get; set; }

        public Guid? BatchId { get; set; }

        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = string.Empty;

        public Guid? ProductVariantId { get; set; }

        public string? VariantTitle { get; set; }

        public string? MaskedCode { get; set; }

        public string? SerialNumber { get; set; }

        public byte Status { get; set; }

        public Guid? ReservedByUserId { get; set; }

        public DateTime? ReservedAt { get; set; }

        public DateTime? SoldAt { get; set; }

        public DateTime? DeliveredAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public Guid? OrderItemId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
