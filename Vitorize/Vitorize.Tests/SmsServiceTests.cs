using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public class SmsServiceTests
{
    private static SmsOptions Enabled(Dictionary<string, int>? templates = null) => new()
    {
        IsEnabled = true,
        ApiKey = "test-key",
        DefaultLineNumber = 30001234,
        TemplateIds = templates ?? new Dictionary<string, int> { [SmsTemplateKeys.LoginOtp] = 111 },
        MaxRetryCount = 3
    };

    private static SmsService Build(SmsOptions options, FakeSmsSender sender) =>
        new(new FakeSmsSettingsProvider(options), sender, NullLogger<SmsService>.Instance);

    private static SmsTemplateParameter[] ValidOtpParameters() =>
    [
        new(SmsTemplateParams.Code, "123456"),
        new(SmsTemplateParams.Expire, "3")
    ];

    [Fact]
    public async Task SendTemplate_WhenDisabled_ReturnsDisabled_AndDoesNotSend()
    {
        var sender = new FakeSmsSender();
        var svc = Build(new SmsOptions { IsEnabled = false, ApiKey = "k" }, sender);

        var result = await svc.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
            new[] { new SmsTemplateParameter("CODE", "123456") });

        Assert.False(result.IsSuccess);
        Assert.Equal(SmsFailureReason.Disabled, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }

    [Fact]
    public async Task SendTemplate_WhenApiKeyMissing_ReturnsNotConfigured()
    {
        var sender = new FakeSmsSender();
        var svc = Build(new SmsOptions { IsEnabled = true, ApiKey = "" }, sender);

        var result = await svc.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
            ValidOtpParameters());

        Assert.Equal(SmsFailureReason.NotConfigured, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }

    [Fact]
    public async Task SendTemplate_InvalidMobile_ReturnsInvalidMobile()
    {
        var sender = new FakeSmsSender();
        var svc = Build(Enabled(), sender);

        var result = await svc.SendTemplateAsync("123", SmsTemplateKeys.LoginOtp,
            Array.Empty<SmsTemplateParameter>());

        Assert.Equal(SmsFailureReason.InvalidMobile, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }

    [Fact]
    public async Task SendTemplate_MissingTemplateId_ReturnsInvalidTemplate()
    {
        var sender = new FakeSmsSender();
        var svc = Build(Enabled(new Dictionary<string, int>()), sender); // no templates configured

        var result = await svc.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
            ValidOtpParameters());

        Assert.Equal(SmsFailureReason.InvalidTemplate, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }

    [Fact]
    public async Task SendOtp_MapsCodeAndExpireParameters_AndNormalizesMobile()
    {
        var sender = new FakeSmsSender();
        var svc = Build(Enabled(), sender);

        var result = await svc.SendOtpAsync("+989123456789", SmsTemplateKeys.LoginOtp, "135790", 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, sender.VerifyCallCount);
        Assert.Equal("09123456789", sender.LastMobile);   // normalized
        Assert.Equal(111, sender.LastTemplateId);
        Assert.Contains(sender.LastParameters!, p => p is { Name: "CODE", Value: "135790" });
        Assert.Contains(sender.LastParameters!, p => p is { Name: "EXPIRE", Value: "3" });
    }

    [Fact]
    public async Task SendTemplate_TransientFailure_IsRetriedThenSucceeds()
    {
        var sender = new FakeSmsSender();
        sender.EnqueueVerifyResult(SmsSendResult.Failure(SmsFailureReason.Network));
        sender.EnqueueVerifyResult(SmsSendResult.Success("99", 10m));
        var svc = Build(Enabled(), sender);

        var result = await svc.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
            ValidOtpParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sender.VerifyCallCount);          // retried once
        Assert.Equal("99", result.ProviderMessageId);
    }

    [Fact]
    public async Task SendTemplate_NonTransientFailure_IsNotRetried()
    {
        var sender = new FakeSmsSender();
        sender.SetDefaultVerify(SmsSendResult.Failure(SmsFailureReason.InsufficientCredit));
        var svc = Build(Enabled(), sender);

        var result = await svc.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
            ValidOtpParameters());

        Assert.False(result.IsSuccess);
        Assert.Equal(SmsFailureReason.InsufficientCredit, result.FailureReason);
        Assert.Equal(1, sender.VerifyCallCount);          // no retry
    }

    [Fact]
    public async Task SendText_WithoutLineNumber_ReturnsInvalidLineNumber()
    {
        var sender = new FakeSmsSender();
        var options = new SmsOptions { IsEnabled = true, ApiKey = "k", DefaultLineNumber = null };
        var svc = Build(options, sender);

        var result = await svc.SendTextAsync("09123456789", "hello");

        Assert.Equal(SmsFailureReason.InvalidLineNumber, result.FailureReason);
        Assert.Equal(0, sender.BulkCallCount);
    }

    [Fact]
    public async Task ValidateConfiguration_MissingTemplate_ReportsInvalid()
    {
        var sender = new FakeSmsSender();
        var svc = Build(Enabled(new Dictionary<string, int>()), sender);

        var (isValid, _) = await svc.ValidateConfigurationAsync();

        Assert.False(isValid);
    }

    [Fact]
    public async Task SendTemplate_OtpWithWrongParameterNames_IsRejectedBeforeSender()
    {
        var sender = new FakeSmsSender();
        var svc = Build(Enabled(), sender);

        var result = await svc.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
        [
            new("code", "123456"),
            new(SmsTemplateParams.Expire, "3")
        ]);

        Assert.Equal(SmsFailureReason.InvalidParameter, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }

    [Fact]
    public async Task SendTemplate_NotificationRequiresOnlyOrderNumber()
    {
        var sender = new FakeSmsSender();
        var options = Enabled(new Dictionary<string, int>
        {
            [SmsTemplateKeys.UniversalNotification] = 222
        });
        var svc = Build(options, sender);
        var parameters = SmsBusinessNotificationParameters.OrderPaid("VT-1");

        var result = await svc.SendTemplateAsync(
            "09123456789", SmsTemplateKeys.UniversalNotification, parameters);

        Assert.True(result.IsSuccess);
        Assert.Equal(222, sender.LastTemplateId);
        Assert.Equal(new[] { "ORDER_NUMBER" },
            sender.LastParameters!.Select(x => x.Name));
    }

    [Fact]
    public async Task ValidateConfiguration_RequiresBothUniversalTemplates()
    {
        var sender = new FakeSmsSender();
        var svc = Build(Enabled(new Dictionary<string, int>
        {
            [SmsTemplateKeys.GenericOtp] = 111,
            [SmsTemplateKeys.UniversalNotification] = 222
        }), sender);

        var (isValid, _) = await svc.ValidateConfigurationAsync();

        Assert.True(isValid);
    }
}
