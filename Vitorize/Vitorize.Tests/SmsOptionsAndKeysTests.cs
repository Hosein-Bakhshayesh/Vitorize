using Vitorize.Application.Common;
using Vitorize.Application.Models.Sms;
using Xunit;

namespace Vitorize.Tests;

public class SmsOptionsAndKeysTests
{
    [Fact]
    public void GetTemplateId_ReturnsConfiguredId()
    {
        var opts = new SmsOptions
        {
            TemplateIds = new Dictionary<string, int> { [SmsTemplateKeys.LoginOtp] = 42 }
        };

        Assert.Equal(42, opts.GetTemplateId(SmsTemplateKeys.LoginOtp));
        Assert.Null(opts.GetTemplateId(SmsTemplateKeys.OrderPaid));
    }

    [Fact]
    public void GetTemplateId_ZeroOrNegative_TreatedAsUnset()
    {
        var opts = new SmsOptions
        {
            TemplateIds = new Dictionary<string, int> { [SmsTemplateKeys.LoginOtp] = 0 }
        };

        Assert.Null(opts.GetTemplateId(SmsTemplateKeys.LoginOtp));
    }

    [Theory]
    [InlineData("user", "password", true)]
    [InlineData("", "password", false)]
    [InlineData("user", "", false)]
    [InlineData(null, "password", false)]
    public void IsOperational_RequiresAsanakCredentials(string? username, string? password, bool expected)
    {
        var opts = new SmsOptions { AsanakUsername = username, AsanakPassword = password };
        Assert.Equal(expected, opts.IsOperational);
    }

    [Theory]
    [InlineData("user", "password", "982100000000", true)]
    [InlineData("user", "password", null, false)]
    [InlineData("", "password", "982100000000", false)]
    public void CanSendText_RequiresCredentialsAndSource(
        string? username, string? password, string? source, bool expected)
    {
        var opts = new SmsOptions
        {
            AsanakUsername = username,
            AsanakPassword = password,
            AsanakSource = source
        };

        Assert.Equal(expected, opts.CanSendText);
        Assert.Equal(expected, opts.CanSendNotificationText);
    }

    [Fact]
    public void SecretKeys_IncludeAsanakCredentialsAndSource()
    {
        Assert.Contains(SmsSettingKeys.AsanakUsername, SmsSettingKeys.SecretKeys);
        Assert.Contains(SmsSettingKeys.AsanakPassword, SmsSettingKeys.SecretKeys);
        Assert.Contains(SmsSettingKeys.AsanakSource, SmsSettingKeys.SecretKeys);
        Assert.DoesNotContain("Sms.IsEnabled", SmsSettingKeys.SecretKeys);
    }

    [Theory]
    [InlineData("Sms.Provider")]
    [InlineData("Sms.ApiKey")]
    [InlineData("Sms.DefaultLineNumber")]
    [InlineData("Sms.SenderName")]
    public void Legacy_provider_settings_are_hidden_until_database_cleanup(string key)
    {
        Assert.Contains(key, SmsSettingKeys.DeprecatedKeys);
    }
}
