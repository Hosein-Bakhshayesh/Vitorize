namespace Vitorize.Application.Models.Sms
{
    /// <summary>
    /// پارامتر قالب SMS.ir؛ فقط CODE/EXPIRE برای OTP و ORDER_NUMBER برای اعلان تجاری مجاز است.
    /// </summary>
    public sealed record SmsTemplateParameter(string Name, string Value);
}
