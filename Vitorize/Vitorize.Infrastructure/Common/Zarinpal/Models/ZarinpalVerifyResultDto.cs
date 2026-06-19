namespace Vitorize.Infrastructure.Common.Zarinpal.Models
{
    public class ZarinpalVerifyResultDto
    {
        public ZarinpalVerifyData? data { get; set; }

        public List<object>? errors { get; set; }
    }

    public class ZarinpalVerifyData
    {
        public int code { get; set; }

        public long ref_id { get; set; }

        public decimal fee { get; set; }

        public string? fee_type { get; set; }
    }
}