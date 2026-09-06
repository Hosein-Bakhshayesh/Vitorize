using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms;

/// <summary>Chooses the configured transport per message without falling back across providers.</summary>
public sealed class SmsSenderRouter : ISmsSender
{
    private readonly SmsIrSender _smsIr;
    private readonly AsanakSmsSender _asanak;

    public SmsSenderRouter(SmsIrSender smsIr, AsanakSmsSender asanak)
    {
        _smsIr = smsIr;
        _asanak = asanak;
    }

    public Task<SmsSendResult> SendVerifyAsync(SmsOptions options, string mobile, int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters, CancellationToken cancellationToken = default) =>
        Select(options) switch
        {
            SmsIrSender smsIr => smsIr.SendVerifyAsync(options, mobile, templateId, parameters, cancellationToken),
            AsanakSmsSender asanak => asanak.SendVerifyAsync(options, mobile, templateId, parameters, cancellationToken),
            _ => Task.FromResult(SmsSendResult.Failure(SmsFailureReason.NotConfigured, "ارائه‌دهنده پیامک معتبر نیست."))
        };

    public Task<SmsSendResult> SendBulkAsync(SmsOptions options, string text, string mobile,
        CancellationToken cancellationToken = default) =>
        Select(options) switch
        {
            SmsIrSender smsIr => smsIr.SendBulkAsync(options, text, mobile, cancellationToken),
            AsanakSmsSender asanak => asanak.SendBulkAsync(options, text, mobile, cancellationToken),
            _ => Task.FromResult(SmsSendResult.Failure(SmsFailureReason.NotConfigured, "ارائه‌دهنده پیامک معتبر نیست."))
        };

    public Task<SmsAccountStatus> GetAccountStatusAsync(SmsOptions options, CancellationToken cancellationToken = default) =>
        Select(options) switch
        {
            SmsIrSender smsIr => smsIr.GetAccountStatusAsync(options, cancellationToken),
            AsanakSmsSender asanak => asanak.GetAccountStatusAsync(options, cancellationToken),
            _ => Task.FromResult(new SmsAccountStatus { IsSuccess = false, UserMessage = "ارائه‌دهنده پیامک معتبر نیست." })
        };

    private object? Select(SmsOptions options) =>
        SmsProviderNames.IsSmsIr(options.Provider) ? _smsIr :
        SmsProviderNames.IsAsanak(options.Provider) ? _asanak : null;
}
