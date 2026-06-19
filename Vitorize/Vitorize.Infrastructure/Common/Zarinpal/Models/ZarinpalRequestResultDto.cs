namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalRequestResultDto
    {
        public ZarinpalRequestData? data { get; set; }

        public List<object>? errors { get; set; }
    }

    public class ZarinpalRequestData
    {
        public string authority { get; set; } = string.Empty;

        public int code { get; set; }

        public string? fee_type { get; set; }

        public decimal? fee { get; set; }
    }
}