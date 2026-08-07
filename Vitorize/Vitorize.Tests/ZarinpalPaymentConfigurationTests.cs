using Vitorize.Infrastructure.Common.Zarinpal;
using Xunit;

namespace Vitorize.Tests;

public sealed class ZarinpalPaymentConfigurationTests
{
    private const string MerchantId = "7d424aa5-0776-4aae-99c4-c71f704e1154";

    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Installation_placeholder_is_recognized_and_never_a_merchant_id(string merchantId)
    {
        Assert.True(ZarinpalPaymentConfigurationRules.IsDeploymentPlaceholder(merchantId));
    }

    [Fact]
    public void Live_configuration_with_https_callback_is_valid()
    {
        var result = ZarinpalPaymentConfigurationRules.Validate(Configuration(
            sandbox: false,
            baseUrl: "https://payment.zarinpal.com/pg/v4/payment",
            startUrl: "https://payment.zarinpal.com/pg/StartPay",
            callbackUrl: "https://api.vitorize.example/api/payments/zarinpal/callback"), production: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Production_rejects_sandbox_endpoint()
    {
        var result = ZarinpalPaymentConfigurationRules.Validate(Configuration(
            sandbox: true,
            baseUrl: "https://sandbox.zarinpal.com/pg/v4/payment",
            startUrl: "https://sandbox.zarinpal.com/pg/StartPay",
            callbackUrl: "https://localhost/api/payments/zarinpal/callback"), production: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("sandbox", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("not-a-guid", "https://payment.zarinpal.com/pg/v4/payment", "https://payment.zarinpal.com/pg/StartPay")]
    [InlineData("7d424aa5-0776-4aae-99c4-c71f704e1154", "https://sandbox.zarinpal.com/pg/v4/payment", "https://payment.zarinpal.com/pg/StartPay")]
    public void Invalid_merchant_or_mixed_gateway_environment_is_rejected(string merchantId, string baseUrl, string startUrl)
    {
        var result = ZarinpalPaymentConfigurationRules.Validate(new ZarinpalPaymentConfiguration(
            merchantId, false, new Uri(baseUrl), new Uri(startUrl),
            new Uri("https://api.vitorize.example/api/payments/zarinpal/callback")), production: true);

        Assert.False(result.IsValid);
    }

    private static ZarinpalPaymentConfiguration Configuration(bool sandbox, string baseUrl, string startUrl, string callbackUrl) =>
        new(MerchantId, sandbox, new Uri(baseUrl), new Uri(startUrl), new Uri(callbackUrl));
}
