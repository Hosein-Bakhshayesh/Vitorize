namespace Vitorize.Application.DTOs.Admin.Sms
{
    public class SendTestSmsRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        /// <summary>
        /// کلید منطقی قالب؛ فقط OTP با CODE/EXPIRE می‌پذیرد.
        /// اگر خالی باشد از متن ساده استفاده می‌شود.
        /// </summary>
        public string? TemplateKey { get; set; }

        public string? Text { get; set; }

        public List<TestSmsParameterDto> Parameters { get; set; } = new();
    }

    public class TestSmsParameterDto
    {
        /// <summary>نام دقیق و حساس به حروف متغیر تاییدشده قالب آسانک.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>مقدار غیرخالی متغیر قالب.</summary>
        public string Value { get; set; } = string.Empty;
    }
}
