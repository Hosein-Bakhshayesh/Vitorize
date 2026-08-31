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
    [InlineData("key", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOperational_RequiresApiKey(string? apiKey, bool expected)
    {
        var opts = new SmsOptions { ApiKey = apiKey };
        Assert.Equal(expected, opts.IsOperational);
    }

    [Theory]
    [InlineData("key", 30001234, true)]
    [InlineData("key", null, false)]
    [InlineData("key", 0, false)]
    [InlineData(null, 30001234, false)]
    public void CanSendText_RequiresApiKeyAndDedicatedLine(
        string? apiKey, int? lineNumber, bool expected)
    {
        var opts = new SmsOptions
        {
            ApiKey = apiKey,
            DefaultLineNumber = lineNumber
        };

        Assert.Equal(expected, opts.CanSendText);
        Assert.Equal(expected, opts.CanSendNotificationText);
    }

    [Fact]
    public void SecretKeys_IncludeApiKeyAndLineNumber()
    {
        Assert.Contains(SmsSettingKeys.ApiKey, SmsSettingKeys.SecretKeys);
        Assert.Contains(SmsSettingKeys.DefaultLineNumber, SmsSettingKeys.SecretKeys);
        Assert.DoesNotContain("Sms.IsEnabled", SmsSettingKeys.SecretKeys);
    }
}
