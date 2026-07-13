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
    [InlineData(true, "key", true)]
    [InlineData(false, "key", false)]
    [InlineData(true, "", false)]
    [InlineData(true, null, false)]
    public void IsOperational_RequiresEnabledAndApiKey(bool enabled, string? apiKey, bool expected)
    {
        var opts = new SmsOptions { IsEnabled = enabled, ApiKey = apiKey };
        Assert.Equal(expected, opts.IsOperational);
    }

    [Fact]
    public void SecretKeys_IncludeApiKeyAndLineNumber()
    {
        Assert.Contains(SmsSettingKeys.ApiKey, SmsSettingKeys.SecretKeys);
        Assert.Contains(SmsSettingKeys.DefaultLineNumber, SmsSettingKeys.SecretKeys);
        Assert.DoesNotContain(SmsSettingKeys.IsEnabled, SmsSettingKeys.SecretKeys);
    }
}
