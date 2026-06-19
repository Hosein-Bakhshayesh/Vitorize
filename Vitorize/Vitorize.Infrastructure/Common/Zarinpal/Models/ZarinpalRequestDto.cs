namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalRequestDto
    {
        public string merchant_id { get; set; } = string.Empty;

        public decimal amount { get; set; }

        public string callback_url { get; set; } = string.Empty;

        public string description { get; set; } = string.Empty;

        public Dictionary<string, string>? metadata { get; set; }
    }
}