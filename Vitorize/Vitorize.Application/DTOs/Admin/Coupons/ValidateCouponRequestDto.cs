namespace Vitorize.Application.DTOs.Coupons
{
    public class ValidateCouponRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public decimal OrderAmount { get; set; }
    }
}