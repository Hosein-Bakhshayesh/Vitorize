namespace Vitorize.Application.DTOs.Coupons
{
    public class CouponDto
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public byte DiscountType { get; set; }

        public decimal DiscountValue { get; set; }

        public int? MaxUsageCount { get; set; }

        public int UsedCount { get; set; }

        public int? MaxUsagePerUser { get; set; }

        public decimal? MinOrderAmount { get; set; }

        public DateTime? StartsAt { get; set; }

        public DateTime? EndsAt { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}