namespace Vitorize.Application.DTOs.Admin.Coupons
{
    public class AdminCouponUpdateDto
    {
        public string Title { get; set; } = string.Empty;

        public byte DiscountType { get; set; }

        public decimal DiscountValue { get; set; }

        public int? MaxUsageCount { get; set; }

        public int? MaxUsagePerUser { get; set; }

        public decimal? MinOrderAmount { get; set; }

        public DateTime? StartsAt { get; set; }

        public DateTime? EndsAt { get; set; }

        public bool IsActive { get; set; }
    }
}