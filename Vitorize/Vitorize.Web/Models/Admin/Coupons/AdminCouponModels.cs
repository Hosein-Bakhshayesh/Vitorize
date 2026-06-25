using System.ComponentModel.DataAnnotations;
using Vitorize.Shared.Enums;

namespace Vitorize.Web.Models.Admin.Coupons
{
    public class AdminCouponModel
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

    public class AdminCouponInputModel
    {
        [Required, MaxLength(100)] public string Code { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [Range(1, 2)] public byte DiscountType { get; set; } = (byte)Vitorize.Shared.Enums.DiscountType.Percentage;
        [Range(0, double.MaxValue)] public decimal DiscountValue { get; set; }
        public int? MaxUsageCount { get; set; }
        public int? MaxUsagePerUser { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
