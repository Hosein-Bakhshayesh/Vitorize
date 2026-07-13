using System.Text;

namespace Vitorize.Application.Common
{
    /// <summary>
    /// نرمال‌سازی و اعتبارسنجی متمرکز شماره موبایل ایران.
    /// این تنها منبع حقیقت برای فرمت شماره است؛ منطق نرمال‌سازی نباید در جای دیگری تکرار شود.
    /// خروجی استاندارد داخلی: «09XXXXXXXXX» (۱۱ رقم).
    /// </summary>
    public static class IranMobile
    {
        /// <summary>
        /// ورودی‌های مختلف (۰۹...، ۹...، ‎+۹۸...، ۰۰۹۸...، همراه با فاصله/خط تیره/ارقام فارسی)
        /// را به فرمت استاندارد «09XXXXXXXXX» تبدیل می‌کند.
        /// </summary>
        public static bool TryNormalize(string? input, out string normalized)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            // تبدیل ارقام فارسی/عربی به لاتین و حذف هر کاراکتر غیرعددی (به‌جز + که جداگانه مدیریت می‌شود).
            var digits = ExtractDigits(input, out var hadPlus);

            if (digits.Length == 0)
                return false;

            // حالت‌های پیشوند کد کشور را به «9XXXXXXXXX» (۱۰ رقم) کاهش می‌دهیم.
            if (hadPlus && digits.StartsWith("98"))
                digits = digits.Substring(2);
            else if (digits.StartsWith("0098"))
                digits = digits.Substring(4);
            else if (digits.StartsWith("98") && digits.Length == 12)
                digits = digits.Substring(2);
            else if (digits.StartsWith("0"))
                digits = digits.Substring(1);

            // اکنون باید دقیقاً ۱۰ رقم و شروع‌شونده با ۹ باشد.
            if (digits.Length != 10 || digits[0] != '9')
                return false;

            if (!IsAllDigits(digits))
                return false;

            normalized = "0" + digits;
            return true;
        }

        public static bool IsValid(string? input) => TryNormalize(input, out _);

        /// <summary>
        /// شماره را برای نمایش امن پنهان می‌کند: «0912***4567».
        /// ورودی نامعتبر → «***».
        /// </summary>
        public static string Mask(string? input)
        {
            if (!TryNormalize(input, out var n))
                return "***";

            // 09XX***XXXX
            return $"{n.Substring(0, 4)}***{n.Substring(7)}";
        }

        /// <summary>فرمت بین‌المللی «98XXXXXXXXXX» (بدون + و بدون صفر ابتدایی).</summary>
        public static bool TryToInternational(string? input, out string international)
        {
            international = string.Empty;
            if (!TryNormalize(input, out var n))
                return false;

            international = "98" + n.Substring(1);
            return true;
        }

        private static string ExtractDigits(string input, out bool hadPlus)
        {
            hadPlus = input.TrimStart().StartsWith("+");
            var sb = new StringBuilder(input.Length);

            foreach (var ch in input)
            {
                var mapped = MapDigit(ch);
                if (mapped >= 0)
                    sb.Append((char)('0' + mapped));
            }

            return sb.ToString();
        }

        private static int MapDigit(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            // ارقام فارسی ۰..۹
            if (ch >= '۰' && ch <= '۹') return ch - '۰';
            // ارقام عربی ٠..٩
            if (ch >= '٠' && ch <= '٩') return ch - '٠';
            return -1;
        }

        private static bool IsAllDigits(string s)
        {
            foreach (var c in s)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }
    }
}
