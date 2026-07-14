using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Validators.Admin;
using Xunit;

namespace Vitorize.Tests;

public class SendTestSmsRequestValidatorTests
{
    private readonly SendTestSmsRequestValidator _validator = new();

    [Fact]
    public async Task Notification_WithOrderNumber_IsValid()
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

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("TITLE")]
    [InlineData("REFERENCE")]
    [InlineData("DETAIL")]
    [InlineData("UNKNOWN")]
    public async Task Notification_WithOldOrUnknownParameter_IsInvalid(string invalidName)
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Notification",
            Parameters =
            [
                new() { Name = "ORDER_NUMBER", Value = "VT-123456" },
                new() { Name = invalidName, Value = "VT-123456" }
            ]
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Notification_WithoutOrderNumber_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Notification",
            Parameters = []
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Notification_WithDuplicateOrderNumber_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SendTestSmsRequestDto
        {
            Mobile = "09123456789",
            TemplateKey = "Notification",
            Parameters =
            [
                new() { Name = "ORDER_NUMBER", Value = "VT-1" },
                new() { Name = "ORDER_NUMBER", Value = "VT-2" }
            ]
        });

        Assert.False(result.IsValid);
    }
}
