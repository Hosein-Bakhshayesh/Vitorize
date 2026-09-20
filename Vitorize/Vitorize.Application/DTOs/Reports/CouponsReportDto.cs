namespace Vitorize.Application.DTOs.Admin.Reports
{
    public class CouponsReportDto
    {
        public int TotalCoupons { get; set; }

        public int ActiveCoupons { get; set; }

        public int TotalUsages { get; set; }

        /// <summary>Discount granted by successful orders whose coupon usage falls in the selected range.</summary>
        public decimal TotalDiscount { get; set; }

        public List<CouponUsageReportDto> TopCoupons { get; set; } = new();
    }

    public class CouponUsageReportDto
    {
        public Guid CouponId { get; set; }

        public string Code { get; set; } = null!;

        public string Title { get; set; } = null!;

        public int UsageCount { get; set; }
    }
}
