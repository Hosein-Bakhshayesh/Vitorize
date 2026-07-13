using Vitorize.Application.Common;
using Xunit;

namespace Vitorize.Tests;

public class OtpSecurityTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(4)]
    [InlineData(8)]
    public void Generate_ProducesRequestedNumberOfDigits(int digits)
    {
        for (var i = 0; i < 200; i++)
        {
            var code = OtpSecurity.Generate(digits);
            Assert.Equal(digits, code.Length);
            Assert.All(code, c => Assert.InRange(c, '0', '9'));
        }
    }

    [Fact]
    public void Generate_ClampsOutOfRangeDigits()
    {
        Assert.Equal(4, OtpSecurity.Generate(1).Length);
        Assert.Equal(8, OtpSecurity.Generate(99).Length);
    }

    [Fact]
    public void Hash_IsDeterministicAndNotPlaintext()
    {
        var hash = OtpSecurity.Hash("123456");
        Assert.Equal(hash, OtpSecurity.Hash("123456"));
        Assert.NotEqual("123456", hash);
    }

    [Fact]
    public void Hash_DifferentCodesProduceDifferentHashes()
    {
        Assert.NotEqual(OtpSecurity.Hash("123456"), OtpSecurity.Hash("654321"));
    }

    [Fact]
    public void Verify_CorrectCode_ReturnsTrue()
    {
        var hash = OtpSecurity.Hash("135790");
        Assert.True(OtpSecurity.Verify("135790", hash));
    }

    [Fact]
    public void Verify_WrongCode_ReturnsFalse()
    {
        var hash = OtpSecurity.Hash("135790");
        Assert.False(OtpSecurity.Verify("000000", hash));
        Assert.False(OtpSecurity.Verify("", hash));
    }
}
