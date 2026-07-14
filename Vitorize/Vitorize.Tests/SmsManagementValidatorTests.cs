using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Validators.Admin;
using Xunit;

namespace Vitorize.Tests;

public sealed class SmsManagementValidatorTests
{
    [Fact]
    public async Task NotificationValidator_AcceptsOneSafePublicReference()
    {
        var request = new SendCustomNotificationRequestDto
        {
            Mobile = "09123456789",
            OrderNumber = "VT-20260713-ABC123"
        };
        var result = await new SendCustomNotificationRequestValidator().ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("VT 123")]
    [InlineData("شناسه-داخلی")]
    public async Task NotificationValidator_RejectsMissingOrUnsafePublicReference(string reference)
    {
        var request = new SendCustomNotificationRequestDto
        {
            Mobile = "09123456789",
            OrderNumber = reference
        };
        var result = await new SendCustomNotificationRequestValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("line\u0001break")]
    public async Task TextValidator_RejectsEmptyHtmlAndControlCharacters(string text)
    {
        var request = new SendCustomTextRequestDto
        {
            Mobile = "09123456789",
            Text = text
        };
        var result = await new SendCustomTextRequestValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TextValidator_AcceptsPlainPersianText()
    {
        var request = new SendCustomTextRequestDto
        {
            Mobile = "+989123456789",
            Text = "اطلاع‌رسانی آزمایشی ویتورایز"
        };
        var result = await new SendCustomTextRequestValidator().ValidateAsync(request);

        Assert.True(result.IsValid);
    }
}
