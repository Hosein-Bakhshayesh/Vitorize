namespace Vitorize.Application.DTOs.Admin.Sms
{
    /// <summary>نتیجه امن ارسال پیامک آزمایشی برای نمایش به ادمین (بدون افشای اطلاعات محرمانه).</summary>
    public class SmsTestResultDto
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public decimal? Cost { get; set; }
        public string? FailureReason { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
