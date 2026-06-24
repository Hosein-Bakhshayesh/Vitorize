using System.Text.Json;

namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalVerifyResultDto
    {
        public ZarinpalVerifyDataDto? data { get; set; }

        public JsonElement errors { get; set; }
    }

    public class ZarinpalVerifyDataDto
    {
        public int code { get; set; }

        public string? message { get; set; }

        public string? card_hash { get; set; }

        public string? card_pan { get; set; }

        public long ref_id { get; set; }

        public string? fee_type { get; set; }

        public int? fee { get; set; }
    }
}