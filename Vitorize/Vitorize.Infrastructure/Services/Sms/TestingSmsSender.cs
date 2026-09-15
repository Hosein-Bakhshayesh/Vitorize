using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services.Testing;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms;

/// <summary>
/// Deterministic in-process SMS transport for the isolated Testing host only.
/// Registration is guarded by Testing:UseFakeSms and no message content is logged.
/// Supports opt-in, Testing-environment-only fault injection (<see cref="TestingFaultInjectionOptions"/>).
/// </summary>
public sealed class TestingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, CapturedTemplate> _latest =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CapturedText> _latestText =
        new(StringComparer.Ordinal);
    private static readonly Regex OtpCodePattern =
        new(@"(?<![0-9])(?<code>[0-9]{6})(?![0-9])", RegexOptions.Compiled);
    private static readonly Regex ExpiryPattern =
        new(@"(?<minutes>[0-9]{1,3})\s*دقیقه", RegexOptions.Compiled);
    private readonly IOptionsMonitor<TestingFaultInjectionOptions> _faults;
    private readonly bool _faultInjectionAllowed;

    public TestingSmsSender(
        IOptionsMonitor<TestingFaultInjectionOptions> faults,
        IHostEnvironment environment)
    {
        _faults = faults;
        // Hard guard: fault injection is impossible outside the Testing environment.
        _faultInjectionAllowed = environment.IsEnvironment("Testing");
    }

    public async Task<SmsSendResult> SendVerifyAsync(
        SmsOptions options,
        string mobile,
        int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        if (await TryInjectSmsFaultAsync(cancellationToken) is { } fault)
            return fault;

        if (IranMobile.TryNormalize(mobile, out var normalized))
            _latest[normalized] = new CapturedTemplate(templateId, parameters.ToArray(), DateTime.UtcNow);

        return SmsSendResult.Success($"testing-{Guid.NewGuid():N}");
    }

    public async Task<SmsSendResult> SendBulkAsync(
        SmsOptions options,
        string text,
        string mobile,
        CancellationToken cancellationToken = default)
    {
        if (await TryInjectSmsFaultAsync(cancellationToken) is { } fault)
            return fault;

        // OTPs travel as plain text since template sending was removed, so the code has to be
        // recovered from the message body. Only messages that actually carry a six-digit group are
        // captured, so an unrelated notification cannot overwrite a code the caller is waiting for.
        if (IranMobile.TryNormalize(mobile, out var normalized) &&
            OtpCodePattern.Match(text ?? string.Empty) is { Success: true } match)
        {
            _latestText[normalized] = new CapturedText(
                match.Groups["code"].Value,
                ExpiryPattern.Match(text!) is { Success: true } expiry ? expiry.Groups["minutes"].Value : string.Empty,
                DateTime.UtcNow);
        }

        return SmsSendResult.Success($"testing-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Returns a configured failure (optionally after an artificial delay) when Testing-only fault
    /// injection is enabled; otherwise null so the caller proceeds with a normal success.
    /// </summary>
    private async Task<SmsSendResult?> TryInjectSmsFaultAsync(CancellationToken cancellationToken)
    {
        if (!_faultInjectionAllowed)
            return null;

        var options = _faults.CurrentValue;
        if (!options.IsSmsFaultRequested)
            return null;

        if (options.DelayMs > 0)
            await Task.Delay(options.DelayMs, cancellationToken);

        var reason = options.Sms.Trim().ToLowerInvariant() switch
        {
            "network" => SmsFailureReason.Network,
            "timeout" => SmsFailureReason.Timeout,
            "unavailable" => SmsFailureReason.ProviderUnavailable,
            _ => SmsFailureReason.Unknown, // "fail" and any other value: non-transient failure
        };
        return SmsSendResult.Failure(reason);
    }

    public Task<SmsAccountStatus> GetAccountStatusAsync(
        SmsOptions options,
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
        if (!IranMobile.TryNormalize(mobile, out var normalized))
            return false;

        if (_latest.TryGetValue(normalized, out var captured))
        {
            code = captured.Parameters.FirstOrDefault(x =>
                x.Name.Equals(SmsTemplateParams.Code, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            expire = captured.Parameters.FirstOrDefault(x =>
                x.Name.Equals(SmsTemplateParams.Expire, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        }

        if (_latestText.TryGetValue(normalized, out var capturedText) &&
            (captured is null || capturedText.CapturedAtUtc >= captured.CapturedAtUtc))
        {
            code = capturedText.Code;
            expire = capturedText.Expire;
        }

        return code.Length > 0;
    }

    private sealed record CapturedTemplate(
        int TemplateId,
        IReadOnlyList<SmsTemplateParameter> Parameters,
        DateTime CapturedAtUtc);

    private sealed record CapturedText(string Code, string Expire, DateTime CapturedAtUtc);
}
