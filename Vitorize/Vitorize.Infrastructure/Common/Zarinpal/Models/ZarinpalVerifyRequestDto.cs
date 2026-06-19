namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalVerifyRequestDto
    {
        public string merchant_id { get; set; } = string.Empty;

        public decimal amount { get; set; }

        public string authority { get; set; } = string.Empty;
    }
}