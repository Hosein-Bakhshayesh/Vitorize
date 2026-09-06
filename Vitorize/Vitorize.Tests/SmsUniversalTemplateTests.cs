using Vitorize.Application.Common;
using Xunit;

namespace Vitorize.Tests;

public class SmsUniversalTemplateTests
{
    [Fact]
    public void AllTemplateSettingsAreDeprecatedAndCannotBeSynchronized()
    {
        Assert.False(SmsSettingKeys.TryGetTemplateIdGroup(SmsSettingKeys.OtpTemplateId, out _));
        Assert.Contains(SmsSettingKeys.OtpTemplateId, SmsSettingKeys.DeprecatedKeys);
        Assert.Contains(SmsSettingKeys.NotificationTemplateId, SmsSettingKeys.DeprecatedKeys);
    }

    [Fact]
    public void AutomaticTemplatePolicyBlocksEveryTemplate()
    {
        Assert.False(SmsAutomaticEventPolicy.IsAllowedTemplate(SmsTemplateKeys.LoginOtp));
        Assert.False(SmsAutomaticEventPolicy.IsAllowedTemplate(SmsTemplateKeys.TicketReply));
    }

    [Fact]
    public void OtpTextAndSystemNotificationsUseTheStandardFooter()
    {
        var messages = new[]
        {
            SmsNotificationMessages.Otp("123456", 3),
            OrderSmsMessages.Processing("VT-1"),
            SmsNotificationMessages.WalletTopUpSucceeded(50_000, "WL-1"),
            SmsNotificationMessages.TicketReply("TK-1")
        };

        Assert.All(messages, message => Assert.EndsWith(SmsNotificationMessages.Footer, message));
    }

    [Theory]
    [InlineData(SmsTemplateKeys.WalletTopUpSuccess, "WL-12", "شارژ کیف پول")]
    [InlineData(SmsTemplateKeys.TicketReply, "TK-12", "پاسخ جدیدی برای تیکت")]
    public void LegacyNotificationPayloadsConvertToSafeText(string templateKey, string reference, string expectedText)
    {
        var text = SmsNotificationMessages.LegacyNotification(templateKey, reference);

        Assert.Contains(expectedText, text);
        Assert.EndsWith(SmsNotificationMessages.Footer, text);
    }
}
