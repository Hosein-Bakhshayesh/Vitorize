using System.Collections.Concurrent;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;

namespace Vitorize.Infrastructure.Services.Sms;

/// <summary>
/// Deterministic in-process SMS transport for the isolated Testing host only.
/// Registration is guarded by Testing:UseFakeSms and no message content is logged.
/// </summary>
public sealed class TestingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, CapturedTemplate> _latest =
        new(StringComparer.Ordinal);

    public Task<SmsSendResult> SendVerifyAsync(
        string apiKey,
        string mobile,
        int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        if (IranMobile.TryNormalize(mobile, out var normalized))
            _latest[normalized] = new CapturedTemplate(templateId, parameters.ToArray(), DateTime.UtcNow);

        return Task.FromResult(SmsSendResult.Success($"testing-{Guid.NewGuid():N}"));
    }

    public Task<SmsSendResult> SendBulkAsync(
        string apiKey,
        long lineNumber,
        string text,
        string mobile,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SmsSendResult.Success($"testing-{Guid.NewGuid():N}"));

    public Task<SmsAccountStatus> GetAccountStatusAsync(
        string apiKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsAccountStatus
        {
            IsSuccess = true,
            Credit = 1_000_000,
            Lines = [3_000_000_000]
        });

    public bool TryGetLatestOtp(string? mobile, out string code, out string expire)
    {
        code = string.Empty;
        expire = string.Empty;
        if (!IranMobile.TryNormalize(mobile, out var normalized) ||
            !_latest.TryGetValue(normalized, out var captured))
            return false;

        code = captured.Parameters.FirstOrDefault(x =>
            x.Name.Equals(SmsTemplateParams.Code, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        expire = captured.Parameters.FirstOrDefault(x =>
            x.Name.Equals(SmsTemplateParams.Expire, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        return code.Length > 0;
    }

    private sealed record CapturedTemplate(
        int TemplateId,
        IReadOnlyList<SmsTemplateParameter> Parameters,
        DateTime CapturedAtUtc);
}
