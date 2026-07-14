using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Vitorize.Application.DTOs.Products;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

public static class ProductInputRules
{
    private static readonly Regex SafeKey = new("^[a-z][a-z0-9_]{1,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Email = new("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Telegram = new("^@?[A-Za-z0-9_]{5,32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void ValidateDefinition(ProductInputFieldDto field)
    {
        field.Key = (field.Key ?? string.Empty).Trim().ToLowerInvariant();
        field.Label = (field.Label ?? string.Empty).Trim();
        if (!SafeKey.IsMatch(field.Key))
            throw new BusinessException("نام داخلی فیلد باید با حرف لاتین شروع شود و فقط شامل حروف کوچک، عدد و زیرخط باشد.");
        if (field.Label.Length is < 1 or > 120)
            throw new BusinessException("عنوان فیلد باید بین ۱ تا ۱۲۰ نویسه باشد.");
        if (!Enum.IsDefined(typeof(ProductInputFieldType), field.FieldType))
            throw new BusinessException("نوع فیلد معتبر نیست.");
        if (!Enum.IsDefined(typeof(ProductInputStage), field.DisplayStage))
            throw new BusinessException("مرحله نمایش فیلد معتبر نیست.");
        if ((field.Description?.Length ?? 0) > 500 || (field.Placeholder?.Length ?? 0) > 200)
            throw new BusinessException("متن راهنما یا placeholder بیش از حد طولانی است.");
        if (field.MinLength is < 0 || field.MaxLength is < 1 or > 2000 ||
            field.MinLength.HasValue && field.MaxLength.HasValue && field.MinLength > field.MaxLength)
            throw new BusinessException("محدوده طول فیلد معتبر نیست.");
        if ((field.ValidationPattern?.Length ?? 0) > 200)
            throw new BusinessException("الگوی اعتبارسنجی بیش از حد طولانی است.");
        if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
        {
            try { _ = new Regex(field.ValidationPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)); }
            catch (ArgumentException) { throw new BusinessException("الگوی اعتبارسنجی معتبر نیست."); }
        }

        var type = (ProductInputFieldType)field.FieldType;
        if (type is ProductInputFieldType.Select or ProductInputFieldType.Radio)
        {
            field.Options = field.Options.Select(x => x?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Take(50).ToList();
            if (field.Options.Count == 0 || field.Options.Any(x => x.Length > 120))
                throw new BusinessException("برای فیلد انتخابی حداقل یک گزینه معتبر الزامی است.");
        }
        else field.Options = new List<string>();

        if (type == ProductInputFieldType.Secret && !field.IsSensitive)
            throw new BusinessException("فیلد محرمانه باید به‌عنوان داده حساس علامت‌گذاری شود.");
    }

    public static string? ValidateValue(ProductInputFieldDto field, string? raw)
    {
        var value = raw?.Trim();
        var type = (ProductInputFieldType)field.FieldType;
        if (type == ProductInputFieldType.Checkbox)
            value = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" ? "true" : "false";

        if (field.IsRequired && (string.IsNullOrWhiteSpace(value) || type == ProductInputFieldType.Checkbox && value != "true"))
            throw new BusinessException(field.ValidationMessage ?? $"تکمیل «{field.Label}» الزامی است.");
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (field.MinLength.HasValue && value.Length < field.MinLength || field.MaxLength.HasValue && value.Length > field.MaxLength)
            throw new BusinessException(field.ValidationMessage ?? $"طول مقدار «{field.Label}» معتبر نیست.");

        var valid = type switch
        {
            ProductInputFieldType.Email => Email.IsMatch(value),
            ProductInputFieldType.Mobile => IranMobile.IsValid(value),
            ProductInputFieldType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            ProductInputFieldType.TelegramUsername => Telegram.IsMatch(value),
            ProductInputFieldType.Url => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
            ProductInputFieldType.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            ProductInputFieldType.Select or ProductInputFieldType.Radio => field.Options.Contains(value, StringComparer.Ordinal),
            _ => true
        };
        if (!valid) throw new BusinessException(field.ValidationMessage ?? $"مقدار «{field.Label}» معتبر نیست.");

        if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
        {
            try
            {
                if (!Regex.IsMatch(value, field.ValidationPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
                    throw new BusinessException(field.ValidationMessage ?? $"مقدار «{field.Label}» معتبر نیست.");
            }
            catch (RegexMatchTimeoutException) { throw new BusinessException("اعتبارسنجی فیلد در زمان مجاز انجام نشد."); }
        }
        return type == ProductInputFieldType.Mobile && IranMobile.TryNormalize(value, out var mobile) ? mobile : value;
    }

    public static string Fingerprint(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var canonical = string.Join("\n", values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value ?? string.Empty}"));
        return canonical.Length == 0 ? "NONE" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "••••";
        return value.Length <= 4 ? new string('•', value.Length) : $"{value[..2]}••••{value[^2..]}";
    }
}
