using System.Globalization;

namespace Vitorize.Application.Common;

/// <summary>
/// Store-wide KYC policy. The threshold is always stored in Toman; a zero or invalid value disables
/// the rule. The checkout snapshots this decision on the order items, so later setting changes never
/// change an existing purchase.
/// </summary>
public static class OrderKycSettings
{
    public static class Keys
    {
        public const string ThresholdToman = "Verification.OrderAmountThresholdToman";
        public const string CustomerNotice = "Verification.CustomerNotice";
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ThresholdToman, CustomerNotice
        };
    }

    public const decimal DefaultThresholdToman = 1_000_000m;

    public const string DefaultCustomerNotice =
        "به دلیل حساسیت‌های اخیر پلیس محترم فتا و جهت جلوگیری از جرایم و کلاهبرداری الکترونیکی، فروشگاه اینترنتی ویتورایز ناچار است تا هویت فردی مشتریان خود را تأیید کند. اطلاعات شما نزد فروشگاه ویتورایز محفوظ خواهند ماند و این مراحل صرفاً جهت جلوگیری از کلاهبرداری‌های اینترنتی و موارد فیشینگ و سایبری است؛ بنابراین می‌توانید با خیال راحت احراز هویت خود را انجام دهید و در کمترین زمان ممکن نتیجه از طریق پیامک به شما اعلام خواهد شد.";

    public static OrderKycSettingsSnapshot Resolve(string? thresholdValue, string? noticeValue)
    {
        var threshold = decimal.TryParse(thresholdValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? decimal.Round(parsed, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var notice = string.IsNullOrWhiteSpace(noticeValue) ? DefaultCustomerNotice : noticeValue.Trim();
        return new OrderKycSettingsSnapshot(threshold, notice);
    }

    public static void ValidateSetting(string? key, string? value)
    {
        if (!string.Equals(key, Keys.ThresholdToman, StringComparison.OrdinalIgnoreCase))
            return;

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold) || threshold < 0)
            throw new ArgumentException("آستانه مبلغ احراز هویت باید یک عدد صفر یا بزرگ‌تر باشد.");
    }

    public static decimal ThresholdForCurrency(OrderKycSettingsSnapshot settings, byte currencyType) =>
        currencyType == 1 ? settings.ThresholdToman * 10m : settings.ThresholdToman;
}

public sealed record OrderKycSettingsSnapshot(decimal ThresholdToman, string CustomerNotice)
{
    public bool IsEnabled => ThresholdToman > 0;
}
