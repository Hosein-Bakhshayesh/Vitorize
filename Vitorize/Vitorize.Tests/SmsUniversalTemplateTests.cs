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
    public void Otp_uses_its_compact_footer_and_system_notifications_use_the_standard_footer()
    {
        var notifications = new[]
        {
            OrderSmsMessages.Processing("VT-1"),
            SmsNotificationMessages.WalletTopUpSucceeded(50_000, "WL-1"),
            SmsNotificationMessages.TicketReply("TK-1")
        };

        Assert.EndsWith(SmsNotificationMessages.OtpFooter, SmsNotificationMessages.Otp("123456", 3));
        Assert.All(notifications, message => Assert.EndsWith(SmsNotificationMessages.Footer, message));
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
