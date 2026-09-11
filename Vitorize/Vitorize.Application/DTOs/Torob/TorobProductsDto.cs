using System.Text.Json.Serialization;

namespace Vitorize.Application.DTOs.Torob;

/// <summary>
/// Normalised Torob API v3 request. The API layer reads the wire body leniently (JSON regardless of
/// Content-Type, form or query-string bodies, stringified numbers, extra keys) and produces exactly one
/// lookup mode: <c>PageUrls</c>, <c>PageUniques</c>, cursor (<c>Sort == "product_id_desc"</c>) or numbered
/// (<c>Page</c> + <c>Sort</c>). <c>Sort</c> is canonical lower-case; lists are trimmed and non-empty.
/// </summary>
public sealed class TorobProductsRequest
{
    [JsonPropertyName("page")]
    public int? Page { get; set; }

    [JsonPropertyName("sort")]
    public string? Sort { get; set; }

    [JsonPropertyName("page_urls")]
    public List<string>? PageUrls { get; set; }

    [JsonPropertyName("page_uniques")]
    public List<string>? PageUniques { get; set; }

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

public sealed class TorobProductsResponse
{
    [JsonPropertyName("api_version")]
    public string ApiVersion { get; init; } = "torob_api_v3";

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>Torob's schema appendix names this field <c>count</c> while its examples use <c>total</c>; both are emitted.</summary>
    [JsonPropertyName("count")]
    public int Count => Total;

    [JsonPropertyName("max_pages")]
    public int MaxPages { get; init; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    [JsonPropertyName("products")]
    public IReadOnlyList<TorobProductDto> Products { get; init; } = [];
}

/// <summary>
/// One Torob offer. Every documented key is always present: Torob's appendix declares the optional
/// fields as <c>Optional[...]</c> (nullable), not as omissible, so nulls are written explicitly.
/// </summary>
public sealed class TorobProductDto
{
    [JsonPropertyName("page_unique")]
    public string PageUnique { get; init; } = string.Empty;

    [JsonPropertyName("page_url")]
    public string PageUrl { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; init; }

    [JsonPropertyName("product_group_id")]
    public string ProductGroupId { get; init; } = string.Empty;

    /// <summary>Toman, integer. Torob compares it with the price shown on <see cref="PageUrl"/>.</summary>
    [JsonPropertyName("current_price")]
    public long CurrentPrice { get; init; }

    [JsonPropertyName("old_price")]
    public long? OldPrice { get; init; }

    [JsonPropertyName("availability")]
    public bool Availability { get; init; }

    [JsonPropertyName("image_links")]
    public IReadOnlyList<string> ImageLinks { get; init; } = [];

    [JsonPropertyName("spec")]
    public IReadOnlyDictionary<string, string> Spec { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; init; }

    [JsonPropertyName("short_desc")]
    public string? ShortDescription { get; init; }

    [JsonPropertyName("guarantee")]
    public string? Guarantee { get; init; }

    /// <summary>ISO 8601 with seconds precision and an explicit zone, e.g. <c>2026-09-03T15:35:38+00:00</c>.</summary>
    [JsonPropertyName("date_added")]
    public string DateAdded { get; init; } = string.Empty;

    [JsonPropertyName("date_updated")]
    public string DateUpdated { get; init; } = string.Empty;
}

public sealed class TorobErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}
