using FluentAssertions;
using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class OrderKycSettingsTests
{
    [Fact]
    public void Threshold_is_disabled_when_the_setting_is_missing_or_invalid()
    {
        OrderKycSettings.Resolve(null, null).IsEnabled.Should().BeFalse();
        OrderKycSettings.Resolve("invalid", null).IsEnabled.Should().BeFalse();
        OrderKycSettings.Resolve("-1", null).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Threshold_is_stored_in_toman_and_converted_only_for_rial_orders()
    {
        var settings = OrderKycSettings.Resolve("1000000", null);

        OrderKycSettings.ThresholdForCurrency(settings, (byte)CurrencyType.Toman).Should().Be(1_000_000m);
        OrderKycSettings.ThresholdForCurrency(settings, (byte)CurrencyType.Rial).Should().Be(10_000_000m);
    }

    [Fact]
    public void Default_customer_notice_is_available_when_an_admin_has_not_customized_it()
    {
        OrderKycSettings.Resolve("1000000", null).CustomerNotice
            .Should().Be(OrderKycSettings.DefaultCustomerNotice);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1000000")]
    [InlineData("1000000.25")]
    public void Non_negative_thresholds_are_valid(string value) =>
        FluentActions.Invoking(() => OrderKycSettings.ValidateSetting(OrderKycSettings.Keys.ThresholdToman, value))
            .Should().NotThrow();

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void Invalid_thresholds_are_rejected(string value) =>
        FluentActions.Invoking(() => OrderKycSettings.ValidateSetting(OrderKycSettings.Keys.ThresholdToman, value))
            .Should().Throw<ArgumentException>();
}
