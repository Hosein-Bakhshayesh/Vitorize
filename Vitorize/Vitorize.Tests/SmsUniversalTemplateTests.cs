using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services.Sms;
using Xunit;

namespace Vitorize.Tests;

public class SmsUniversalTemplateTests
{
    [Fact]
    public void BuildTemplateIds_MapsOnlyTheSingleOtpTemplate()
    {
        var result = SmsSettingsProvider.BuildTemplateIds(new Dictionary<string, string?>
        {
            [SmsSettingKeys.OtpTemplateId] = "101",
            [SmsSettingKeys.LoginOtpTemplateId] = "999",
            [SmsSettingKeys.NotificationTemplateId] = "202"
        });

        Assert.All(SmsTemplateKeys.OtpTemplates, key => Assert.Equal(101, result[key]));
        Assert.DoesNotContain(SmsTemplateKeys.UniversalNotification, result.Keys);
        Assert.Single(result.Values.Distinct());
    }

    [Fact]
    public void BuildTemplateIds_FallsBackToLegacyOtpKeyOnly()
    {
        var result = SmsSettingsProvider.BuildTemplateIds(new Dictionary<string, string?>
        {
            [SmsSettingKeys.RegisterOtpTemplateId] = "303",
            [SmsSettingKeys.TicketReplyTemplateId] = "404"
        });

        Assert.All(SmsTemplateKeys.OtpTemplates, key => Assert.Equal(303, result[key]));
        Assert.DoesNotContain(SmsTemplateKeys.TicketReply, result.Keys);
    }

    [Fact]
    public void OnlyOtpSettingsAreSynchronizedTemplateSettings()
    {
        Assert.True(SmsSettingKeys.TryGetTemplateIdGroup(
            SmsSettingKeys.ForgotPasswordTemplateId, out var group));
        Assert.Same(SmsSettingKeys.OtpTemplateIdKeys, group);
        Assert.False(SmsSettingKeys.TryGetTemplateIdGroup(
            SmsSettingKeys.NotificationTemplateId, out _));
        Assert.Contains(SmsSettingKeys.NotificationTemplateId, SmsSettingKeys.DeprecatedKeys);
    }

    [Theory]
    [InlineData(SmsTemplateKeys.WalletTopUpSuccess, "WL-12", "شارژ کیف پول")]
    [InlineData(SmsTemplateKeys.TicketReply, "TK-12", "پاسخ جدیدی برای تیکت")]
    [InlineData(SmsTemplateKeys.VerificationApproved, "VF-12", "احراز هویت شما با موفقیت")]
    public void LegacyNotificationText_IsSafeAndDoesNotNeedATemplate(
        string templateKey, string reference, string expectedText)
    {
        var text = SmsNotificationMessages.LegacyNotification(templateKey, reference);

        Assert.Contains(expectedText, text);
        Assert.EndsWith(SmsNotificationMessages.Footer, text);
        Assert.DoesNotContain("{{", text);
    }

    [Fact]
    public void AutomaticTemplatePolicy_AllowsOnlyOtp()
    {
        Assert.True(SmsAutomaticEventPolicy.IsAllowedTemplate(SmsTemplateKeys.LoginOtp));
        Assert.False(SmsAutomaticEventPolicy.IsAllowedTemplate(SmsTemplateKeys.TicketReply));
        Assert.False(SmsAutomaticEventPolicy.IsAllowedTemplate(SmsTemplateKeys.WalletTopUpSuccess));
    }

    [Fact]
    public void SystemNotificationMessages_UseTheStandardFooter()
    {
        var messages = new[]
        {
            OrderSmsMessages.Processing("VT-1"),
            OrderSmsMessages.Completed("VT-2"),
            OrderSmsMessages.Cancelled("VT-3"),
            SmsNotificationMessages.WalletTopUpSucceeded(50_000, "WL-1"),
            SmsNotificationMessages.TicketReply("TK-1"),
            SmsNotificationMessages.AdminReferenceNotification("ADMIN-1")
        };

        Assert.All(messages, message => Assert.EndsWith(SmsNotificationMessages.Footer, message));
    }

    [Fact]
    public async Task EveryOtpFlowUsesTheSingleConfiguredTemplate()
    {
        var options = new SmsOptions
        {
            AsanakUsername = "test-user",
            AsanakPassword = "test-password",
            TemplateIds = SmsSettingsProvider.BuildTemplateIds(new Dictionary<string, string?>
            {
                [SmsSettingKeys.OtpTemplateId] = "101"
            }),
            MaxRetryCount = 0
        };

        foreach (var templateKey in SmsTemplateKeys.OtpTemplates)
        {
            var sender = new FakeSmsSender();
            var service = new SmsService(new FakeSmsSettingsProvider(options), sender, NullLogger<SmsService>.Instance);
            var result = await service.SendTemplateAsync("09123456789", templateKey,
            [
                new(SmsTemplateParams.Code, "123456"),
                new(SmsTemplateParams.Expire, "3")
            ]);

            Assert.True(result.IsSuccess, templateKey);
            Assert.Equal(101, sender.LastTemplateId);
        }
    }

    [Fact]
    public async Task NotificationTemplateSend_IsRejected()
    {
        var options = new SmsOptions
        {
            AsanakUsername = "test-user",
            AsanakPassword = "test-password",
            TemplateIds = new Dictionary<string, int> { [SmsTemplateKeys.UniversalNotification] = 202 }
        };
        var sender = new FakeSmsSender();
        var service = new SmsService(new FakeSmsSettingsProvider(options), sender, NullLogger<SmsService>.Instance);

        var result = await service.SendTemplateAsync("09123456789", SmsTemplateKeys.UniversalNotification,
            [new(SmsTemplateParams.OrderNumber, "VT-123456")]);

        Assert.False(result.IsSuccess);
        Assert.Equal(Vitorize.Shared.Enums.SmsFailureReason.InvalidTemplate, result.FailureReason);
        Assert.Equal(0, sender.VerifyCallCount);
    }
}
