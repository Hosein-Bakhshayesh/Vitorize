namespace Vitorize.Application.Models.Sms
{
    /// <summary>
    /// پارامتر قالب آسانکِ تاریخی؛ ارسال‌های جدید از آن استفاده نمی‌کنند.
    /// </summary>
    public sealed record SmsTemplateParameter(string Name, string Value);
}
