namespace Vitorize.Application.DTOs.Outbox
{
    /// <summary>
    /// محتوای پیام Outbox از نوع «SmsSend». توسط پردازشگر پس‌زمینه خوانده و از طریق
    /// سرویس متمرکز پیامک ارسال می‌شود؛ بنابراین شکست ارائه‌دهنده هرگز تراکنش تجاری را برنمی‌گرداند.
    /// </summary>
    public sealed class SmsOutboxPayload
    {
        public string Mobile { get; set; } = string.Empty;

        /// <summary>کلید منطقی قالب؛ اگر مقدار داشته باشد، ارسال قالبی انجام می‌شود.</summary>
        public string? TemplateKey { get; set; }

        public List<SmsOutboxParameter> Parameters { get; set; } = new();

        /// <summary>در صورت نبود قالب، متن ساده ارسال می‌شود.</summary>
        public string? Text { get; set; }

        /// <summary>برچسب رویداد برای لاگ و رفع‌ابهام (مثل «OrderPaid»).</summary>
        public string Purpose { get; set; } = string.Empty;
    }

    public sealed class SmsOutboxParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
