using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.Tests;

/// <summary>تأمین‌کننده تنظیمات جعلی برای تست (بدون DB).</summary>
public sealed class FakeSmsSettingsProvider : ISmsSettingsProvider
{
    private SmsOptions _options;
    public FakeSmsSettingsProvider(SmsOptions options) => _options = options;
    public Task<SmsOptions> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_options);
    public void Invalidate() { }
    public void Set(SmsOptions options) => _options = options;
}

/// <summary>ارائه‌دهنده جعلی پیامک؛ فراخوانی‌ها را ثبت و نتایج از پیش‌تعیین‌شده را برمی‌گرداند.</summary>
public sealed class FakeSmsSender : ISmsSender
{
    public int VerifyCallCount { get; private set; }
    public int BulkCallCount { get; private set; }
    public string? LastUsername { get; private set; }
    public string? LastMobile { get; private set; }
    public int? LastTemplateId { get; private set; }
    public string? LastSource { get; private set; }
    public string? LastText { get; private set; }
    public IReadOnlyList<SmsTemplateParameter>? LastParameters { get; private set; }

    private readonly Queue<SmsSendResult> _verifyResults = new();
    private SmsSendResult _defaultVerify = SmsSendResult.Success("1", 100m, 1, "ok");
    private SmsSendResult _defaultBulk = SmsSendResult.Success("2", 100m, 1, "ok");

    public void EnqueueVerifyResult(SmsSendResult result) => _verifyResults.Enqueue(result);
    public void SetDefaultVerify(SmsSendResult result) => _defaultVerify = result;
    public void SetDefaultBulk(SmsSendResult result) => _defaultBulk = result;

    public Task<SmsSendResult> SendVerifyAsync(
        SmsOptions options, string mobile, int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters, CancellationToken cancellationToken = default)
    {
        VerifyCallCount++;
        LastUsername = options.AsanakUsername;
        LastMobile = mobile;
        LastTemplateId = templateId;
        LastParameters = parameters;
        var result = _verifyResults.Count > 0 ? _verifyResults.Dequeue() : _defaultVerify;
        return Task.FromResult(result);
    }

    public Task<SmsSendResult> SendBulkAsync(
        SmsOptions options, string text, string mobile, CancellationToken cancellationToken = default)
    {
        BulkCallCount++;
        LastUsername = options.AsanakUsername;
        LastMobile = mobile;
        LastSource = options.AsanakSource;
        LastText = text;
        return Task.FromResult(_defaultBulk);
    }

    public Task<SmsAccountStatus> GetAccountStatusAsync(SmsOptions options, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsAccountStatus { IsSuccess = true, Credit = 5000m });
}
