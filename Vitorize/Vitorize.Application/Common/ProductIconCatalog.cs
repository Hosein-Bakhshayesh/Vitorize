using Vitorize.Shared.Icons;

namespace Vitorize.Application.Common;

public static class ProductIconCatalog
{
    public static IReadOnlyList<string> Keys => IconCatalog.Search(null, null, 5000).Select(x => x.Id).ToArray();

    public static bool IsAllowed(string? key) => string.IsNullOrWhiteSpace(key) || IconCatalog.TryNormalizeKey(key, out _);
}
