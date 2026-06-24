using System.Text.Json;

namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalRequestResultDto
    {
        public ZarinpalRequestDataDto? data { get; set; }

        public JsonElement errors { get; set; }
    }

    public class ZarinpalRequestDataDto
    {
        public int code { get; set; }

        public string? message { get; set; }

        public string? authority { get; set; }

        public string? fee_type { get; set; }

        public int? fee { get; set; }
    }
}