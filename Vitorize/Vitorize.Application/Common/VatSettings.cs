using System.Globalization;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

/// <summary>
/// The immutable VAT configuration used by one pricing calculation. Pricing logic never receives
/// raw <c>Setting</c> entities; it receives this value type.
/// </summary>
public sealed record VatSettingsSnapshot(bool Enabled, decimal RatePercent, VatCalculationMode CalculationMode)
{
    /// <summary>Fallback used whenever VAT settings are absent, blank or unparsable.</summary>
    public static VatSettingsSnapshot Disabled { get; } = new(false, 0m, VatCalculationMode.BeforeDiscount);
}

/// <summary>
/// Central VAT settings contract: key names, invariant parsing and administrative validation.
/// The generic <c>ISettingService.GetValueAsync&lt;T&gt;</c> parser is culture-sensitive, so VAT
/// values are always parsed here with <see cref="CultureInfo.InvariantCulture"/> instead.
/// </summary>
public static class VatSettings
{
    public const string Group = "Tax";
    public const decimal MinimumRatePercent = 0m;
    public const decimal MaximumRatePercent = 100m;

    public static class Keys
    {
        public const string Enabled = "VatEnabled";
        public const string RatePercent = "VatRatePercent";
        public const string CalculationMode = "VatCalculationMode";

        public static readonly string[] All = [Enabled, RatePercent, CalculationMode];
    }

    public static bool IsVatKey(string? key) =>
        key is not null && Keys.All.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Persisted representation. Readable invariant strings, never the numeric enum.</summary>
    public static string ToSettingValue(VatCalculationMode mode) => mode switch
    {
        VatCalculationMode.AfterDiscount => nameof(VatCalculationMode.AfterDiscount),
        _ => nameof(VatCalculationMode.BeforeDiscount)
    };

    public static bool TryParseEnabled(string? raw, out bool value)
    {
        value = false;
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (bool.TryParse(text, out value)) return true;
        // The Admin switch and legacy seeds both use 1/0.
        if (text == "1") { value = true; return true; }
        if (text == "0") { value = false; return true; }
        return false;
    }

    public static bool TryParseRatePercent(string? raw, out decimal value)
    {
        value = 0m;
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return false;
        if (parsed < MinimumRatePercent || parsed > MaximumRatePercent) return false;
        value = parsed;
        return true;
    }

    public static bool TryParseCalculationMode(string? raw, out VatCalculationMode mode)
    {
        mode = VatCalculationMode.BeforeDiscount;
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (string.Equals(text, nameof(VatCalculationMode.BeforeDiscount), StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(text, nameof(VatCalculationMode.AfterDiscount), StringComparison.OrdinalIgnoreCase))
        {
            mode = VatCalculationMode.AfterDiscount;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Builds the effective snapshot from raw stored values. Any missing or invalid value degrades to
    /// the safe default so a malformed row can never silently tax a customer.
    /// </summary>
    public static VatSettingsSnapshot Resolve(string? enabled, string? ratePercent, string? calculationMode)
    {
        if (!TryParseEnabled(enabled, out var isEnabled) || !isEnabled)
            return VatSettingsSnapshot.Disabled;
        if (!TryParseRatePercent(ratePercent, out var rate))
            return VatSettingsSnapshot.Disabled;
        if (!TryParseCalculationMode(calculationMode, out var mode))
            mode = VatCalculationMode.BeforeDiscount;
        return new VatSettingsSnapshot(true, rate, mode);
    }

    /// <summary>Administrative validation, invoked from the existing SettingService validation hook.</summary>
    public static void ValidateSetting(string key, string? value)
    {
        if (!IsVatKey(key)) return;
        var trimmedKey = key.Trim();

        if (string.Equals(trimmedKey, Keys.Enabled, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseEnabled(value, out _))
                throw new BusinessException("مقدار «فعال بودن مالیات» باید true یا false باشد.");
            return;
        }

        if (string.Equals(trimmedKey, Keys.RatePercent, StringComparison.OrdinalIgnoreCase))
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text) ||
                !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
                throw new BusinessException("نرخ مالیات باید یک عدد معتبر باشد.");
            if (rate < MinimumRatePercent || rate > MaximumRatePercent)
                throw new BusinessException("نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
            return;
        }

        if (!TryParseCalculationMode(value, out _))
            throw new BusinessException("نحوه محاسبه مالیات معتبر نیست.");
    }
}
