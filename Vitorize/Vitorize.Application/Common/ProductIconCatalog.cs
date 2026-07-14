namespace Vitorize.Application.Common;

public static class ProductIconCatalog
{
    public static readonly IReadOnlyList<string> Keys =
    [
        "gamepad", "monitor", "globe", "shield", "star", "zap", "clock", "truck",
        "gift", "download", "layers", "tag", "check-circle", "box", "smartphone",
        "headphones", "credit-card", "user", "calendar", "lock", "key", "wifi"
    ];

    public static bool IsAllowed(string? key) => string.IsNullOrWhiteSpace(key) ||
        Keys.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase);
}
