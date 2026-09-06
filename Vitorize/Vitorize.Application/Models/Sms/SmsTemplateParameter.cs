namespace Vitorize.Application.Models.Sms
{
    /// <summary>
    /// پارامتر قالب آسانک؛ فقط CODE/EXPIRE برای OTP فعال است.
    /// </summary>
    public sealed record SmsTemplateParameter(string Name, string Value);
}
