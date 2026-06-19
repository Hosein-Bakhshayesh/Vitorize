namespace Vitorize.Infrastructure.Common.Zarinpal
{
    public class ZarinpalSettings
    {
        public string MerchantId { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public string StartPayUrl { get; set; } = string.Empty;

        public string CallbackUrl { get; set; } = string.Empty;

        public string DescriptionPrefix { get; set; } = string.Empty;
    }
}