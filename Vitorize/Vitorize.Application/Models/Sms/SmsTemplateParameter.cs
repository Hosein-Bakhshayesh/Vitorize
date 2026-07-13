namespace Vitorize.Application.Models.Sms
{
    /// <summary>یک پارامتر قالب پیامک (نام متغیر ↔ مقدار).</summary>
    public sealed record SmsTemplateParameter(string Name, string Value);
}
