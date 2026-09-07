using System.Text.Json.Serialization;

namespace Vitorize.Application.DTOs.Torob;

/// <summary>Request contract defined by Torob API v3. Exactly one lookup mode is accepted.</summary>
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
}

public sealed class TorobProductsResponse
{
    [JsonPropertyName("api_version")]
    public string ApiVersion { get; init; } = "torob_api_v3";

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("max_pages")]
    public int MaxPages { get; init; }

    [JsonPropertyName("products")]
    public IReadOnlyList<TorobProductDto> Products { get; init; } = [];
}

public sealed class TorobProductDto
{
    [JsonPropertyName("page_unique")]
    public string PageUnique { get; init; } = string.Empty;

    [JsonPropertyName("page_url")]
    public string PageUrl { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("subtitle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subtitle { get; init; }

    [JsonPropertyName("product_group_id")]
    public string ProductGroupId { get; init; } = string.Empty;

    [JsonPropertyName("current_price")]
    public long CurrentPrice { get; init; }

    [JsonPropertyName("old_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? OldPrice { get; init; }

    [JsonPropertyName("availability")]
    public bool Availability { get; init; }

    [JsonPropertyName("image_links")]
    public IReadOnlyList<string> ImageLinks { get; init; } = [];

    [JsonPropertyName("spec")]
    public IReadOnlyDictionary<string, string> Spec { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("category_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CategoryName { get; init; }

    [JsonPropertyName("short_desc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShortDescription { get; init; }

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
