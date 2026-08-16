namespace Vitorize.Application.Common;

/// <summary>
/// FIX-17: the administrator-configured initial loading image/GIF. Its path is written directly
/// into the boot HTML before any component renders, so it is accepted only when it is an uploaded
/// file in the settings media folder with a known image extension. Anything else — an absolute URL,
/// a data: URI, a protocol-relative host, a traversal, a stale path from another folder — is
/// rejected and the caller falls back to the built-in Vitorize loader.
/// </summary>
public static class LoadingMediaRules
{
    /// <summary>Folder the settings upload endpoint writes to.</summary>
    public const string RequiredPrefix = "/uploads/settings/";

    /// <summary>
    /// Extensions permitted for the loading visual. GIF is included here and nowhere else in the
    /// product: this is the only image that is allowed to be animated.
    /// </summary>
    public static readonly string[] AllowedExtensions =
    [
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    ];

    /// <summary>
    /// True when the configured value is a safe, uploaded loading medium.
    /// An empty value is not "unsafe" — it simply means "use the default loader" — but it is not a
    /// usable path either, so it returns false and the caller renders the default.
    /// </summary>
    public static bool IsSafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var value = path.Trim();

        // Control characters (including CR/LF) would allow markup smuggling into the boot HTML.
        if (value.Any(char.IsControl)) return false;

        // Backslashes are normalised to forward slashes by some browsers, so reject rather than repair.
        if (value.Contains('\\')) return false;

        // Traversal must never escape the settings media folder.
        if (value.Contains("..", StringComparison.Ordinal)) return false;

        // Defence in depth: refuse anything parseable as absolute (http:, https:, data:, file:).
        if (Uri.TryCreate(value, UriKind.Absolute, out _)) return false;

        if (!value.StartsWith(RequiredPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        // A query or fragment would let arbitrary text ride along into the emitted attribute.
        if (value.Contains('?') || value.Contains('#')) return false;

        // There must be an actual file name after the folder prefix.
        var fileName = value[RequiredPrefix.Length..];
        if (fileName.Length == 0 || fileName.Contains('/')) return false;

        return AllowedExtensions.Any(ext => value.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
