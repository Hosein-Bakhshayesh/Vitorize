using Vitorize.Application.Common;
using Xunit;

namespace Vitorize.Tests;

public class IranMobileTests
{
    [Theory]
    [InlineData("09123456789", "09123456789")]
    [InlineData("9123456789", "09123456789")]
    [InlineData("+989123456789", "09123456789")]
    [InlineData("00989123456789", "09123456789")]
    [InlineData("0912 345 6789", "09123456789")]
    [InlineData("0912-345-6789", "09123456789")]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹", "09123456789")]      // Persian digits
    [InlineData("٠٩١٢٣٤٥٦٧٨٩", "09123456789")]      // Arabic digits
    public void TryNormalize_ValidInputs_ReturnsCanonical(string input, string expected)
    {
        var ok = IranMobile.TryNormalize(input, out var normalized);
        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0812345678")]        // wrong prefix
    [InlineData("091234567")]         // too short
    [InlineData("091234567890")]      // too long
    [InlineData("08123456789")]       // does not start with 9 after 0
    [InlineData("hello")]
    [InlineData("+1234567890")]       // non-Iranian
    public void TryNormalize_InvalidInputs_ReturnsFalse(string? input)
    {
        var ok = IranMobile.TryNormalize(input, out var normalized);
        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void Mask_HidesMiddleDigits()
    {
        Assert.Equal("0912***6789", IranMobile.Mask("09123456789"));
    }

    [Fact]
    public void Mask_InvalidInput_ReturnsStars()
    {
        Assert.Equal("***", IranMobile.Mask("not-a-number"));
    }

    [Fact]
    public void TryToInternational_ReturnsNineEightPrefix()
    {
        var ok = IranMobile.TryToInternational("09123456789", out var intl);
        Assert.True(ok);
        Assert.Equal("989123456789", intl);
    }

    [Fact]
    public void IsValid_MatchesTryNormalize()
    {
        Assert.True(IranMobile.IsValid("09123456789"));
        Assert.False(IranMobile.IsValid("123"));
    }
}
