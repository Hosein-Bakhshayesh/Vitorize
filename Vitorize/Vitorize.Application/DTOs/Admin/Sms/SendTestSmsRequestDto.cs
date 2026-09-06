namespace Vitorize.Application.DTOs.Admin.Sms
{
    public class SendTestSmsRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        /// <summary>
        /// مسیر قالبی حذف شده است و این مقدار فقط برای ردکردن درخواست‌های قدیمی باقی مانده است.
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
