using Vitorize.Shared.Icons;

namespace Vitorize.Application.Common;

public static class ProductIconCatalog
{
    public static IReadOnlyList<string> Keys => LucideIconCatalog.Entries.Select(x => x.Key).ToArray();

    public static bool IsAllowed(string? key) => string.IsNullOrWhiteSpace(key) || LucideIconCatalog.TryNormalizeKey(key, out _);
}
