namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalRequestDto
    {
        public string merchant_id { get; set; } = string.Empty;

        public decimal amount { get; set; }

        public string currency { get; set; } = "IRT";

        public string description { get; set; } = string.Empty;

        public string callback_url { get; set; } = string.Empty;

        public string? referrer_id { get; set; }

        public ZarinpalMetadataDto metadata { get; set; } = new();
    }

    public class ZarinpalMetadataDto
    {
        public string? mobile { get; set; }

        public string? email { get; set; }

        public string? order_id { get; set; }
    }
}