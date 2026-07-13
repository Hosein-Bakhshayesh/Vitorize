namespace Vitorize.Application.DTOs.Admin.Sms
{
    public class SendTestSmsRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        /// <summary>کلید منطقی قالب (مثل LoginOtp)؛ اگر خالی باشد از متن ساده استفاده می‌شود.</summary>
        public string? TemplateKey { get; set; }

        public string? Text { get; set; }

        public List<TestSmsParameterDto> Parameters { get; set; } = new();
    }

    public class TestSmsParameterDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
