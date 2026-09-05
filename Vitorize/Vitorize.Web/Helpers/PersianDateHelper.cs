using System.Globalization;

namespace Vitorize.Web.Helpers
{
    /// <summary>
    /// تبدیل امن تاریخ میلادی به شمسی (جلالی).
    /// هرگز روی null، DateTime.MinValue/MaxValue، مقادیر خارج از بازه‌ی
    /// PersianCalendar، رشته‌ی خالی یا ورودی نامعتبر crash نمی‌کند؛
    /// در این موارد مقدار «-» برمی‌گرداند.
    /// </summary>
    public static class PersianDateHelper
    {
        private static readonly PersianCalendar Calendar = new();
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

        public static readonly string[] MonthNames =
        {
            "فروردین", "اردیبهشت", "خرداد",
            "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر",
            "دی", "بهمن", "اسفند"
        };

        public static readonly string[] WeekDayShortNames =
        {
            "ش", "ی", "د", "س", "چ", "پ", "ج"
        };

        public const string Empty = "-";

        /// <summary>
        /// All persisted operational timestamps are UTC. SQL Server returns a
        /// DateTime without its original Kind, so Unspecified values must also
        /// be treated as UTC here rather than as the server's local clock.
        /// </summary>
        public static DateTime ToIranTime(DateTime value)
        {
            var utc = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TehranTimeZone);
        }

        /// <summary>Converts a date/time entered in Tehran local time to UTC for persistence.</summary>
        public static DateTime IranTimeToUtc(DateTime value) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TehranTimeZone);

        public static bool IsValid(DateTime? value)
        {
            if (!value.HasValue)
                return false;

            var v = value.Value;

            if (v == DateTime.MinValue || v == DateTime.MaxValue)
                return false;

            if (v < Calendar.MinSupportedDateTime || v > Calendar.MaxSupportedDateTime)
                return false;

            // تاریخ‌های بسیار قدیمی معمولاً مقدار پیش‌فرض/نامعتبر هستند.
            if (v.Year < 1800)
                return false;

            return true;
        }

        /// <summary>yyyy/MM/dd به‌صورت ارقام فارسی</summary>
        public static string ToShortDate(DateTime? value)
        {
            if (!IsValid(value))
                return Empty;

            try
            {
                var v = ToIranTime(value!.Value);
                var year = Calendar.GetYear(v);
                var month = Calendar.GetMonth(v);
                var day = Calendar.GetDayOfMonth(v);

                return ToPersianDigits($"{year:0000}/{month:00}/{day:00}");
            }
            catch
            {
                return Empty;
            }
        }

        /// <summary>yyyy/MM/dd HH:mm به‌صورت ارقام فارسی</summary>
        public static string ToDateTime(DateTime? value)
        {
            if (!IsValid(value))
                return Empty;

            try
            {
                var v = ToIranTime(value!.Value);
                var year = Calendar.GetYear(v);
                var month = Calendar.GetMonth(v);
                var day = Calendar.GetDayOfMonth(v);

                return ToPersianDigits(
                    $"{year:0000}/{month:00}/{day:00} {v:HH:mm}");
            }
            catch
            {
                return Empty;
            }
        }

        /// <summary>d MonthName yyyy مثل: ۱۴ خرداد ۱۴۰۳</summary>
        public static string ToLongDate(DateTime? value)
        {
            if (!IsValid(value))
                return Empty;

            try
            {
                var v = ToIranTime(value!.Value);
                var year = Calendar.GetYear(v);
                var month = Calendar.GetMonth(v);
                var day = Calendar.GetDayOfMonth(v);

                return ToPersianDigits($"{day} {MonthNames[month - 1]} {year}");
            }
            catch
            {
                return Empty;
            }
        }

        public static (int Year, int Month, int Day)? ToJalaliParts(DateTime? value)
        {
            if (!IsValid(value))
                return null;

            try
            {
                var v = ToIranTime(value!.Value);
                return (Calendar.GetYear(v), Calendar.GetMonth(v), Calendar.GetDayOfMonth(v));
            }
            catch
            {
                return null;
            }
        }

        public static DateTime? ToGregorian(int year, int month, int day)
        {
            try
            {
                if (year < 1 || month < 1 || month > 12 || day < 1)
                    return null;

                var daysInMonth = Calendar.GetDaysInMonth(year, month);

                if (day > daysInMonth)
                    return null;

                return Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
            catch
            {
                return null;
            }
        }

        public static int DaysInMonth(int year, int month)
        {
            try
            {
                if (year < 1 || month < 1 || month > 12)
                    return 31;

                return Calendar.GetDaysInMonth(year, month);
            }
            catch
            {
                return 31;
            }
        }

        /// <summary>اندیس روز هفته شمسی (شنبه = 0 ... جمعه = 6)</summary>
        public static int PersianDayOfWeek(int year, int month, int day)
        {
            var date = ToGregorian(year, month, day);

            if (date == null)
                return 0;

            return ((int)date.Value.DayOfWeek + 1) % 7;
        }

        public static (int Year, int Month, int Day) Today()
        {
            var now = ToIranTime(DateTime.UtcNow);
            return (Calendar.GetYear(now), Calendar.GetMonth(now), Calendar.GetDayOfMonth(now));
        }

        private static TimeZoneInfo ResolveTehranTimeZone()
        {
            // Windows uses the first identifier; Linux containers use IANA.
            try { return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
            catch (InvalidTimeZoneException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
        }

        public static string ToPersianDigits(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var chars = input.ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '0' && chars[i] <= '9')
                    chars[i] = (char)(chars[i] - '0' + '۰');
            }

            return new string(chars);
        }
    }
}
