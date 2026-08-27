using System.Collections.Frozen;

namespace Vitorize.Shared.Icons;

public sealed record IconCollectionInfo(string Prefix, string Title, string PersianTitle, string SpritePath, int Count);

public sealed record IconRef(
    string Id,          // stored value: "wallet" (Lucide, bare) or "tabler:wallet"
    string Prefix,      // "lucide" | "tabler" | "ph"
    string Name,        // symbol id inside the sprite
    string DisplayName,
    string SpritePath,
    string SearchText);

public readonly record struct IconRenderInfo(string SpritePath, string SymbolId, bool Found);

/// <summary>
/// Multi-collection icon facade. Lucide keeps its bare-key storage format for
/// full backward compatibility (existing DB values are Lucide names); additional
/// collections use a namespaced "prefix:name" id. Rendering, search and resolution
/// all flow through here so every consumer stays collection-agnostic.
/// </summary>
public static class IconCatalog
{
    public const string LucidePrefix = "lucide";
    private const string LucideSprite = "/lib/lucide/lucide-sprite.svg";

    private sealed record ExtraCollection(string Prefix, string Title, string PersianTitle, string Sprite, FrozenDictionary<string, IconRef> ByName, IReadOnlyList<IconRef> Ordered);

    private static readonly IReadOnlyList<ExtraCollection> ExtraCollections =
    [
        BuildExtra(IconCatalogTablerData.Prefix, IconCatalogTablerData.Title, IconCatalogTablerData.PersianTitle,
            "/lib/icons/tabler-sprite.svg", IconCatalogTablerData.Items),
        BuildExtra(IconCatalogPhData.Prefix, IconCatalogPhData.Title, IconCatalogPhData.PersianTitle,
            "/lib/icons/ph-sprite.svg", IconCatalogPhData.Items)
    ];

    private static readonly FrozenDictionary<string, ExtraCollection> ExtraByPrefix =
        ExtraCollections.ToFrozenDictionary(x => x.Prefix, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<IconCollectionInfo> Collections { get; } =
    [
        new(LucidePrefix, "Lucide", "لوساید", LucideSprite, LucideIconCatalog.Count),
        .. ExtraCollections.Select(x => new IconCollectionInfo(x.Prefix, x.Title, x.PersianTitle, x.Sprite, x.Ordered.Count))
    ];

    /// <summary>Splits a stored value into (prefix, name). A bare value is Lucide.</summary>
    public static bool TryParse(string? value, out string prefix, out string name)
    {
        prefix = LucidePrefix;
        name = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim();
        var colon = value.IndexOf(':');
        if (colon > 0)
        {
            var candidate = value[..colon].ToLowerInvariant();
            if (candidate == LucidePrefix || ExtraByPrefix.ContainsKey(candidate))
            {
                prefix = candidate;
                name = value[(colon + 1)..];
                return true;
            }
        }

        name = value; // legacy bare Lucide key
        return true;
    }

    /// <summary>Resolves any stored value to a renderable sprite + symbol, with a safe fallback.</summary>
    public static IconRenderInfo Resolve(string? value, string fallbackLucideKey = "circle-question-mark")
    {
        if (TryParse(value, out var prefix, out var name))
        {
            if (prefix == LucidePrefix)
            {
                var normalized = LucideIconCatalog.ResolveOrFallback(name, fallbackLucideKey);
                return new IconRenderInfo(LucideSprite, normalized, LucideIconCatalog.IsOfficialKey(name)
                    || LucideIconCatalog.TryNormalizeKey(name, out _));
            }

            if (ExtraByPrefix.TryGetValue(prefix, out var collection) && collection.ByName.ContainsKey(name))
                return new IconRenderInfo(collection.Sprite, name, true);
        }

        return new IconRenderInfo(LucideSprite, LucideIconCatalog.ResolveOrFallback(fallbackLucideKey), false);
    }

    /// <summary>True when the value maps to a known icon in any collection (not just the fallback).</summary>
    public static bool IsKnown(string? value) => Resolve(value).Found;

    /// <summary>
    /// Validates an icon value from an untrusted request and returns its canonical
    /// stored form. Lucide values remain bare to preserve existing database
    /// values; icons from other collections retain their <c>prefix:name</c> form.
    /// </summary>
    public static bool TryNormalizeKey(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!TryParse(value, out var prefix, out var name)) return false;

        if (prefix == LucidePrefix)
            return LucideIconCatalog.TryNormalizeKey(name, out normalized);

        if (ExtraByPrefix.TryGetValue(prefix, out var collection)
            && collection.ByName.TryGetValue(name, out var icon))
        {
            normalized = icon.Id;
            return true;
        }

        return false;
    }

    public static IconRef? Find(string? value)
    {
        if (!TryParse(value, out var prefix, out var name)) return null;

        if (prefix == LucidePrefix)
        {
            var entry = LucideIconCatalog.Find(name);
            return entry is null ? null : ToRef(entry);
        }

        return ExtraByPrefix.TryGetValue(prefix, out var collection) && collection.ByName.TryGetValue(name, out var iconRef)
            ? iconRef
            : null;
    }

    /// <summary>
    /// Search a single collection (or all). Lucide reuses its rich scorer; extra
    /// collections use token containment on name + keywords.
    /// </summary>
    public static IReadOnlyList<IconRef> Search(string? query, string? collectionPrefix, int maxResults = 300)
    {
        maxResults = Math.Clamp(maxResults, 1, 5000);

        if (string.Equals(collectionPrefix, LucidePrefix, StringComparison.OrdinalIgnoreCase))
            return LucideIconCatalog.Search(query, null, maxResults).Select(ToRef).ToArray();

        if (!string.IsNullOrWhiteSpace(collectionPrefix) && ExtraByPrefix.TryGetValue(collectionPrefix, out var single))
            return SearchExtra(single, query, maxResults);

        // "all" (null/empty/unknown): merge Lucide + extras.
        var results = new List<IconRef>();
        results.AddRange(LucideIconCatalog.Search(query, null, maxResults).Select(ToRef));
        foreach (var collection in ExtraCollections)
            results.AddRange(SearchExtra(collection, query, maxResults));
        return results.Take(maxResults).ToArray();
    }

    private static IReadOnlyList<IconRef> SearchExtra(ExtraCollection collection, string? query, int maxResults)
    {
        var normalized = LucideIconCatalog.NormalizeSearch(query);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return collection.Ordered.Take(maxResults).ToArray();

        return collection.Ordered
            .Select(icon => (icon, score: Score(icon, tokens)))
            .Where(x => x.score >= 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.icon.Name, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(x => x.icon)
            .ToArray();
    }

    private static int Score(IconRef icon, IReadOnlyList<string> tokens)
    {
        var total = 0;
        foreach (var token in tokens)
        {
            if (icon.Name.Equals(token, StringComparison.OrdinalIgnoreCase)) total += 2000;
            else if (icon.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase)) total += 1200;
            else if (icon.SearchText.Contains(token, StringComparison.Ordinal)) total += 800;
            else return -1;
        }
        return total;
    }

    private static IconRef ToRef(LucideIconEntry entry) => new(
        entry.Key, LucidePrefix, entry.Key, entry.EnglishName, LucideSprite,
        LucideIconCatalog.NormalizeSearch($"{entry.Key} {entry.EnglishName} {entry.PersianAliases}"));

    private static ExtraCollection BuildExtra(string prefix, string title, string persian, string sprite,
        (string Name, string Keywords)[] items)
    {
        var refs = items.Select(item =>
        {
            var display = System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(item.Name.Replace('-', ' '));
            var searchText = LucideIconCatalog.NormalizeSearch($"{item.Name} {item.Keywords}");
            return new IconRef($"{prefix}:{item.Name}", prefix, item.Name, display, sprite, searchText);
        }).ToArray();

        return new ExtraCollection(prefix, title, persian, sprite,
            refs.ToFrozenDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase), refs);
    }
}
