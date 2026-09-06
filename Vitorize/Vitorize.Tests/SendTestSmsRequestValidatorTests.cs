using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Validators.Admin;
using Xunit;

namespace Vitorize.Tests;

public class SendTestSmsRequestValidatorTests
{
    private readonly SendTestSmsRequestValidator _validator = new();

    [Fact]
    public async Task TextMessage_IsValid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            Text = "پیام آزمایشی"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task TemplateMessage_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Otp",
            Text = "پیام آزمایشی"
        });

        Assert.False(result.IsValid);
    }
}
