namespace Vitorize.Application.DTOs.Coupons
{
    public class ValidateCouponResultDto
    {
        public Guid CouponId { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal OrderAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }
}