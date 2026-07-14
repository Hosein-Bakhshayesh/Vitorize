using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

public static class TrustSealRules
{
    private static readonly IReadOnlyDictionary<string, string> Hosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Enamad"] = "enamad.ir", ["Ecunion"] = "ecunion.ir", ["Samandehi"] = "samandehi.ir"
    };

    public static void ValidateSetting(string key, string? value)
    {
        if (!key.StartsWith("TrustSeal.", StringComparison.OrdinalIgnoreCase) || !key.EndsWith(".Url", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value)) return;
        var provider = key.Split('.')[1];
        if (!Hosts.TryGetValue(provider, out var allowed) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !(uri.Host.Equals(allowed, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith('.' + allowed, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessException("نشانی تأیید نماد باید HTTPS و متعلق به دامنه رسمی همان ارائه‌دهنده باشد.");
    }
}
