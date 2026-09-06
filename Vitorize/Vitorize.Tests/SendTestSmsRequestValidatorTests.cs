using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Validators.Admin;
using Xunit;

namespace Vitorize.Tests;

public class SendTestSmsRequestValidatorTests
{
    private readonly SendTestSmsRequestValidator _validator = new();

    [Fact]
    public async Task Otp_WithCodeAndExpire_IsValid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Otp",
            Parameters =
            [
                new() { Name = "CODE", Value = "123456" },
                new() { Name = "EXPIRE", Value = "3" }
            ]
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task NotificationTemplate_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Notification",
            Parameters =
            [
                new() { Name = "ORDER_NUMBER", Value = "VT-123456" }
            ]
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Otp_WithoutExpire_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Otp",
            Parameters = [new() { Name = "CODE", Value = "123456" }]
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Otp_WithDuplicateCode_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Otp",
            Parameters =
            [
                new() { Name = "CODE", Value = "123456" },
                new() { Name = "CODE", Value = "654321" }
            ]
        });

        Assert.False(result.IsValid);
    }
}
