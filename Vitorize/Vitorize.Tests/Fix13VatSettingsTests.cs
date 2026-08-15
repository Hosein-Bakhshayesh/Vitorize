using System.Globalization;
using FluentAssertions;
using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FIX-13 VAT settings contract: safe defaults, invariant parsing and administrative validation.
/// </summary>
public sealed class Fix13VatSettingsTests
{
    [Fact]
    public void Missing_settings_resolve_to_the_safe_disabled_default()
    {
        var snapshot = VatSettings.Resolve(null, null, null);

        snapshot.Should().Be(VatSettingsSnapshot.Disabled);
        snapshot.Enabled.Should().BeFalse();
        snapshot.RatePercent.Should().Be(0m);
        snapshot.CalculationMode.Should().Be(VatCalculationMode.BeforeDiscount);
    }

    [Fact]
    public void An_unparsable_rate_disables_vat_rather_than_taxing_arbitrarily()
    {
        VatSettings.Resolve("true", "not-a-number", "AfterDiscount").Should().Be(VatSettingsSnapshot.Disabled);
        VatSettings.Resolve("true", "-1", "AfterDiscount").Should().Be(VatSettingsSnapshot.Disabled);
        VatSettings.Resolve("true", "101", "AfterDiscount").Should().Be(VatSettingsSnapshot.Disabled);
    }

    [Fact]
    public void An_unknown_mode_falls_back_to_before_discount_without_disabling_vat()
    {
        var snapshot = VatSettings.Resolve("true", "10", "Sideways");

        snapshot.Enabled.Should().BeTrue();
        snapshot.RatePercent.Should().Be(10m);
        snapshot.CalculationMode.Should().Be(VatCalculationMode.BeforeDiscount);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void Boolean_representations_used_by_the_admin_switch_and_seeds_all_parse(string raw, bool expected)
    {
        VatSettings.TryParseEnabled(raw, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void Rate_parsing_is_invariant_regardless_of_the_machine_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator; "12.5" must still parse as twelve and a half.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            VatSettings.TryParseRatePercent("12.5", out var germanCulture).Should().BeTrue();
            germanCulture.Should().Be(12.5m);

            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            VatSettings.TryParseRatePercent("12.5", out var persianCulture).Should().BeTrue();
            persianCulture.Should().Be(12.5m);

            VatSettings.Resolve("true", "12.5", "BeforeDiscount").RatePercent.Should().Be(12.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("BeforeDiscount", VatCalculationMode.BeforeDiscount)]
    [InlineData("beforediscount", VatCalculationMode.BeforeDiscount)]
    [InlineData("AfterDiscount", VatCalculationMode.AfterDiscount)]
    [InlineData("afterdiscount", VatCalculationMode.AfterDiscount)]
    public void Valid_modes_parse(string raw, VatCalculationMode expected)
    {
        VatSettings.TryParseCalculationMode(raw, out var mode).Should().BeTrue();
        mode.Should().Be(expected);
    }

    [Fact]
    public void Modes_persist_as_readable_invariant_strings()
    {
        VatSettings.ToSettingValue(VatCalculationMode.BeforeDiscount).Should().Be("BeforeDiscount");
        VatSettings.ToSettingValue(VatCalculationMode.AfterDiscount).Should().Be("AfterDiscount");
        ((byte)VatCalculationMode.BeforeDiscount).Should().Be(1);
        ((byte)VatCalculationMode.AfterDiscount).Should().Be(2);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("101")]
    [InlineData("abc")]
    [InlineData("")]
    public void An_out_of_range_or_malformed_rate_is_rejected_by_admin_validation(string value) =>
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.RatePercent, value))
            .Should().Throw<BusinessException>();

    [Theory]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("10")]
    [InlineData("12.5")]
    [InlineData("100")]
    public void A_valid_rate_passes_admin_validation(string value) =>
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.RatePercent, value))
            .Should().NotThrow();

    [Fact]
    public void An_invalid_mode_is_rejected_by_admin_validation()
    {
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.CalculationMode, "Sideways"))
            .Should().Throw<BusinessException>();
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.CalculationMode, "1"))
            .Should().Throw<BusinessException>();
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.CalculationMode, "AfterDiscount"))
            .Should().NotThrow();
    }

    [Fact]
    public void An_invalid_enabled_flag_is_rejected_by_admin_validation()
    {
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.Enabled, "yes"))
            .Should().Throw<BusinessException>();
        FluentActions.Invoking(() => VatSettings.ValidateSetting(VatSettings.Keys.Enabled, "true"))
            .Should().NotThrow();
    }

    [Fact]
    public void Non_vat_keys_are_untouched_by_vat_validation() =>
        FluentActions.Invoking(() => VatSettings.ValidateSetting("SiteName", "anything at all"))
            .Should().NotThrow();

    [Fact]
    public void Vat_keys_are_recognised_centrally()
    {
        VatSettings.IsVatKey("VatEnabled").Should().BeTrue();
        VatSettings.IsVatKey("VatRatePercent").Should().BeTrue();
        VatSettings.IsVatKey("VatCalculationMode").Should().BeTrue();
        VatSettings.IsVatKey("SiteName").Should().BeFalse();
        VatSettings.IsVatKey(null).Should().BeFalse();
        VatSettings.Group.Should().Be("Tax");
    }
}
