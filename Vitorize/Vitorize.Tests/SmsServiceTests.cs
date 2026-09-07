using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public class SmsServiceTests
{
    private static SmsOptions Enabled() => new()
    {
        AsanakUsername = "test-user",
        AsanakPassword = "test-password",
        AsanakSource = "982100000000",
        MaxRetryCount = 0
    };

    private static SmsService Build(SmsOptions options, FakeSmsSender sender) =>
        new(new FakeSmsSettingsProvider(options), sender, NullLogger<SmsService>.Instance);

    [Fact]
    public async Task SendOtp_UsesPlainTextAndNeverCallsTemplateTransport()
    {
        var sender = new FakeSmsSender();
        var service = Build(Enabled(), sender);

        var result = await service.SendOtpAsync("+989123456789", "135790", 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("09123456789", sender.LastMobile);
        Assert.Equal(1, sender.BulkCallCount);
        Assert.Equal(0, sender.VerifyCallCount);
        Assert.Contains("135790", sender.LastText);
        Assert.EndsWith(SmsNotificationMessages.OtpFooter, sender.LastText);
        Assert.DoesNotContain("باتشکر", sender.LastText);
    }

    [Fact]
    public async Task SendText_WithoutLineNumber_ReturnsInvalidLineNumber()
    {
        var sender = new FakeSmsSender();
        var service = Build(new SmsOptions { AsanakUsername = "user", AsanakPassword = "password" }, sender);

        var result = await service.SendTextAsync("09123456789", "hello");

        Assert.Equal(SmsFailureReason.InvalidLineNumber, result.FailureReason);
        Assert.Equal(0, sender.BulkCallCount);
    }

    [Fact]
    public async Task SendText_AppendsTheStandardFooterOnce()
    {
        var sender = new FakeSmsSender();
        var service = Build(Enabled(), sender);

        await service.SendTextAsync("09123456789", "متن اطلاع‌رسانی");
        Assert.Equal($"متن اطلاع‌رسانی\n\n{SmsNotificationMessages.Footer}", sender.LastText);

        await service.SendTextAsync("09123456789", sender.LastText!);
        Assert.Equal($"متن اطلاع‌رسانی\n\n{SmsNotificationMessages.Footer}", sender.LastText);
    }

    [Fact]
    public async Task TemplateDelivery_IsRejected()
    {
        var sender = new FakeSmsSender();
        var service = Build(Enabled(), sender);

        var result = await service.SendTemplateAsync("09123456789", "Otp", []);

        Assert.Equal(SmsFailureReason.InvalidTemplate, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }

    [Fact]
    public async Task ValidateConfiguration_RequiresTextCredentialsAndSource()
    {
        var sender = new FakeSmsSender();
        var service = Build(Enabled(), sender);

        var (valid, _) = await service.ValidateConfigurationAsync();

        Assert.True(valid);
    }
}
