using System.Text.RegularExpressions;

namespace Vitorize.Shared.Logging;

public static partial class SensitiveLogData
{
    private static readonly string[] SensitiveNames =
    [
        "Password", "PasswordHash", "Secret", "Token", "RefreshToken", "AccessToken",
        "Authorization", "ApiKey", "EncryptionKey", "GiftCode", "DeliveredContent",
        "Otp", "NationalCode", "Kyc", "DocumentContent", "EncryptedValue",
        "SensitiveValue", "Card", "Cookie"
    ];

    public const string Redacted = "[REDACTED]";

    public static bool IsSensitiveProperty(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return false;
        if (propertyName.Equals("Key", StringComparison.OrdinalIgnoreCase)) return true;
        return SensitiveNames.Any(name => propertyName.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    public static string MaskMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return "[unknown]";
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        if (digits.Length < 7) return "***";
        return $"{digits[..Math.Min(3, digits.Length)]}***{digits[^4..]}";
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "[unknown]";
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1) return "***";
        var local = email[..at];
        return $"{local[0]}***@{email[(at + 1)..]}";
    }

    public static string Sanitize(string? value, int maximumLength = 256)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var bounded = value.Length > maximumLength ? value[..maximumLength] : value;
        return ControlCharacters().Replace(bounded, " ").Trim();
    }

    public static string RedactFreeText(string? value, int maximumLength = 1000)
    {
        var safe = Sanitize(value, maximumLength);
        safe = BearerToken().Replace(safe, "Bearer [REDACTED]");
        safe = JwtToken().Replace(safe, Redacted);
        safe = EmailAddress().Replace(safe, match => MaskEmail(match.Value));
        safe = IranMobile().Replace(safe, match => MaskMobile(match.Value));
        safe = NamedSecret().Replace(safe, match => $"{match.Groups[1].Value}={Redacted}");
        return safe;
    }

    public static string SafeExceptionMessage(Exception exception) =>
        $"{exception.GetType().Name}: {RedactFreeText(exception.Message)}";

    [GeneratedRegex("[\\r\\n\\t\\0-\\x08\\x0B\\x0C\\x0E-\\x1F\\x7F]+", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharacters();

    [GeneratedRegex("(?i)Bearer\\s+[A-Za-z0-9._~+\\-/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();

    [GeneratedRegex("\\beyJ[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtToken();

    [GeneratedRegex("(?i)\\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex EmailAddress();

    [GeneratedRegex("(?<!\\d)(?:\\+98|0098|98|0)?9\\d{9}(?!\\d)", RegexOptions.CultureInvariant)]
    private static partial Regex IranMobile();

    [GeneratedRegex("(?i)\\b(password|passwordhash|secret|token|refreshtoken|accesstoken|authorization|apikey|encryptionkey|giftcode|otp|nationalcode|cookie)\\s*[:=]\\s*[^;,\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex NamedSecret();
}
