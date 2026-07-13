using System.Security.Cryptography;
using System.Text;

namespace Vitorize.Application.Common
{
    /// <summary>
    /// ابزار امن کد یکبار‌مصرف: تولید با مولد رمزنگاری، هش SHA-256 و مقایسه زمان‌ثابت.
    /// هرگز کد خام ذخیره نمی‌شود؛ فقط هش نگهداری می‌شود.
    /// </summary>
    public static class OtpSecurity
    {
        /// <summary>تولید کد عددی با مولد امن رمزنگاری (نه Random).</summary>
        public static string Generate(int digits = 6)
        {
            if (digits < 4) digits = 4;
            if (digits > 8) digits = 8;

            var max = (int)Math.Pow(10, digits);
            var value = RandomNumberGenerator.GetInt32(0, max);
            return value.ToString(new string('0', digits));
        }

        public static string Hash(string code)
        {
            var bytes = Encoding.UTF8.GetBytes(code ?? string.Empty);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>مقایسه زمان‌ثابت کد ورودی با هش ذخیره‌شده (ضد حمله زمان‌بندی).</summary>
        public static bool Verify(string code, string storedHash)
        {
            var candidate = Encoding.UTF8.GetBytes(Hash(code));
            var stored = Encoding.UTF8.GetBytes(storedHash ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(candidate, stored);
        }
    }
}
