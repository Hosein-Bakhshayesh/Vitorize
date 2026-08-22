using System.Globalization;

namespace Vitorize.Web.Helpers
{
    /// <summary>
    /// How a Jalali date is typed and read back as text: <c>yyyy/MM/dd</c>.
    ///
    /// The separators are the component's job, not the customer's. <see cref="Mask"/> is the single
    /// definition of that formatting, mirrored by wwwroot/js/persian-date-mask.js so the browser can
    /// apply it per keystroke without a round trip while the server still validates whatever arrives.
    /// Keeping the rule here means it can be tested directly, and the field behaves correctly even if
    /// the script never loads.
    ///
    /// Nothing here decides what a date *means*: conversion and calendar validity are
    /// <see cref="PersianDateHelper"/>'s job, which uses the real <see cref="PersianCalendar"/>.
    /// </summary>
    public static class PersianDateEntry
    {
        /// <summary>Digits of a complete date: four of year, two of month, two of day.</summary>
        private const int CompleteDigitCount = 8;

        /// <summary>
        /// Persian (۰-۹) and Arabic-Indic (٠-٩) digits are accepted as readily as ASCII; everything
        /// else - including separators the customer typed themselves - is discarded, because the
        /// separators are re-inserted by <see cref="Mask"/>.
        /// </summary>
        public static string DigitsOf(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var digits = new System.Text.StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsAsciiDigit(ch)) digits.Append(ch);
                else if (ch is >= '۰' and <= '۹') digits.Append((char)('0' + (ch - '۰')));
                else if (ch is >= '٠' and <= '٩') digits.Append((char)('0' + (ch - '٠')));
            }
            return digits.ToString();
        }

        /// <summary>
        /// Formats whatever has been typed so far: <c>1403</c> stays <c>1403</c>, <c>14031</c> becomes
        /// <c>1403/1</c>, and <c>14031225</c> becomes <c>1403/12/25</c>. Extra digits beyond a complete
        /// date are dropped rather than silently shifting the value.
        /// </summary>
        public static string Mask(string? value)
        {
            var digits = DigitsOf(value);
            if (digits.Length > CompleteDigitCount) digits = digits[..CompleteDigitCount];

            return digits.Length switch
            {
                <= 4 => digits,
                <= 6 => $"{digits[..4]}/{digits[4..]}",
                _ => $"{digits[..4]}/{digits[4..6]}/{digits[6..]}"
            };
        }

        /// <summary>True once enough digits are present to resolve a date.</summary>
        public static bool IsComplete(string? value) => DigitsOf(value).Length == CompleteDigitCount;

        /// <summary>
        /// Resolves typed text to the Gregorian date it denotes, accepting the separated form, bare
        /// digits, or any mix of digit scripts. Returns false for anything the Persian calendar does
        /// not actually contain - month 13, day 0, or 30 Esfand of a common year.
        /// </summary>
        public static bool TryParse(string? value, out DateTime date)
        {
            date = default;
            var digits = DigitsOf(value);
            if (digits.Length != CompleteDigitCount) return false;

            var year = int.Parse(digits[..4], CultureInfo.InvariantCulture);
            var month = int.Parse(digits[4..6], CultureInfo.InvariantCulture);
            var day = int.Parse(digits[6..], CultureInfo.InvariantCulture);

            var converted = PersianDateHelper.ToGregorian(year, month, day);
            if (converted is null) return false;

            date = converted.Value;
            return true;
        }
    }
}
