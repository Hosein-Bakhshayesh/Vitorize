using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// Read-only catalogue adapter for Torob API v3. It intentionally uses the same availability rules
/// as checkout and the storefront, so a product cannot be advertised as purchasable when its SKU
/// has no sellable inventory.
/// </summary>
public sealed class TorobCatalogService : ITorobCatalogService
{
    private const int PageSize = 100;
    private readonly VitorizeDbContext _db;
    private readonly string _storefrontBaseUrl;
    private readonly string _mediaBaseUrl;

    public TorobCatalogService(VitorizeDbContext db, IConfiguration configuration)
    {
        _db = db;
        _storefrontBaseUrl = NormalizeBaseUrl(configuration["Torob:StorefrontBaseUrl"], "https://vitorize.com");
        _mediaBaseUrl = NormalizeBaseUrl(configuration["Torob:MediaBaseUrl"], "https://api.vitorize.com");
    }

    public async Task<TorobProductsResponse> GetProductsAsync(
        TorobProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(product =>
                product.IsActive &&
                !product.IsDeleted &&
                product.Category.IsActive &&
                !product.Category.IsDeleted &&
                (product.RedirectUrl == null || product.RedirectUrl == string.Empty))
            .Select(product => new CatalogProductSource
            {
                Id = product.Id,
                Title = product.Title,
                Slug = product.Slug,
                ShortDescription = product.ShortDescription,
                CategoryTitle = product.Category.Title,
                DeliveryType = product.DeliveryType,
                CurrencyType = product.CurrencyType,
                BasePrice = product.BasePrice,
                DiscountPrice = product.DiscountPrice,
                ForceOutOfStock = product.ForceOutOfStock,
                ThumbnailImagePath = product.ThumbnailImagePath,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                Images = product.ProductImages
                    .OrderBy(image => image.SortOrder)
                    .Select(image => image.ImagePath)
                    .ToList(),
                Features = product.ProductFeatures
                    .Where(feature => feature.IsActive)
                    .OrderBy(feature => feature.SortOrder)
                    .ThenBy(feature => feature.Id)
                    .Select(feature => new CatalogFeatureSource { Title = feature.Title, Value = feature.Value })
                    .ToList(),
                Variants = product.ProductVariants
                    .Where(variant => variant.IsActive)
                    .OrderBy(variant => variant.SortOrder)
                    .ThenBy(variant => variant.Id)
                    .Select(variant => new CatalogVariantSource
                    {
                        Id = variant.Id,
                        Title = variant.Title,
                        Price = variant.Price,
                        DiscountPrice = variant.DiscountPrice,
                        StockMode = variant.StockMode,
                        StockQuantity = variant.StockQuantity,
                        IsDefault = variant.IsDefault,
                        SortOrder = variant.SortOrder,
                        CreatedAt = variant.CreatedAt,
                        UpdatedAt = variant.UpdatedAt,
                        AvailableGiftCodes = product.DeliveryType == (byte)DeliveryType.Instant
                            ? _db.GiftCodes.Count(code =>
                                code.ProductId == product.Id &&
                                code.ProductVariantId == variant.Id &&
                                code.Status == (byte)GiftCodeStatus.Available)
                            : 0
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Torob's list response pages *products*, while a Vitorize product with real variants must
        // be exported as multiple purchasable offers. Flatten before sorting/paging to keep the
        // documented 100-item page size correct.
        var catalogue = products
            .SelectMany(BuildOffers)
            // An image is mandatory in Torob v3. Excluding incomplete entries is safer than making
            // the entire feed invalid because an administrator has not uploaded an image yet.
            .Where(product => product.ImageLinks.Count > 0)
            .ToList();

        IReadOnlyList<TorobProductDto> selected;
        if (request.PageUrls is { Count: > 0 })
        {
            var requested = request.PageUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(NormalizeRequestedUrl)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            selected = catalogue.Where(product => requested.Contains(NormalizeRequestedUrl(product.PageUrl))).ToList();
        }
        else if (request.PageUniques is { Count: > 0 })
        {
            var requested = request.PageUniques
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);
            selected = catalogue.Where(product => requested.Contains(product.PageUnique)).ToList();
        }
        else
        {
            var sortByUpdate = string.Equals(request.Sort, "date_updated_desc", StringComparison.Ordinal);
            selected = (sortByUpdate
                    ? catalogue.OrderByDescending(product => product.DateUpdated)
                    : catalogue.OrderByDescending(product => product.DateAdded))
                .ThenBy(product => product.PageUnique, StringComparer.Ordinal)
                .ToList();
        }

        var total = selected.Count;
        var page = request.Page ?? 1;
        var maxPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var pageProducts = request.Page.HasValue
            ? selected.Skip((page - 1) * PageSize).Take(PageSize).ToList()
            : selected;

        return new TorobProductsResponse
        {
            CurrentPage = page,
            Total = total,
            MaxPages = maxPages,
            Products = pageProducts
        };
    }

    private IEnumerable<TorobProductDto> BuildOffers(CatalogProductSource product)
    {
        var namedVariants = product.Variants
            .Where(variant => ProductAvailabilityRules.CustomerFacingVariantTitle(variant.Title) is not null)
            .ToList();
        var variants = namedVariants.Count > 0
            ? namedVariants
            : product.Variants.Where(variant => variant.IsDefault).Take(1).ToList();

        // Legacy products can temporarily have no SKU. They remain a valid single offer and use
        // their product-level price, but report unavailable until a sellable SKU exists.
        if (variants.Count == 0)
        {
            yield return CreateOffer(product, null, product.Title);
            yield break;
        }

        foreach (var variant in variants)
        {
            var title = ProductAvailabilityRules.CustomerFacingVariantTitle(variant.Title);
            yield return CreateOffer(product, variant, title is null ? product.Title : $"{product.Title} — {title}");
        }
    }

    private TorobProductDto CreateOffer(CatalogProductSource product, CatalogVariantSource? variant, string title)
    {
        var basePrice = variant?.Price ?? product.BasePrice;
        var discountPrice = variant?.DiscountPrice ?? product.DiscountPrice;
        var finalPrice = discountPrice.HasValue &&
                         discountPrice.Value > 0 &&
                         discountPrice.Value < basePrice
            ? discountPrice.Value
            : basePrice;
        var availability = variant is not null && ProductAvailabilityRules.IsAvailableForSale(
            product.ForceOutOfStock,
            product.DeliveryType,
            (ProductVariantStockMode)variant.StockMode,
            variant.AvailableGiftCodes,
            variant.StockQuantity);
        var pageUnique = variant is null ? $"product:{product.Id:D}" : $"variant:{variant.Id:D}";
        var pageUrl = $"{_storefrontBaseUrl}/product/{Uri.EscapeDataString(product.Slug)}";
        if (variant is not null)
            pageUrl += $"?variant={variant.Id:D}";

        var createdAt = variant is null ? product.CreatedAt : Max(product.CreatedAt, variant.CreatedAt);
        var updatedAt = Max(product.UpdatedAt ?? product.CreatedAt, variant?.UpdatedAt ?? variant?.CreatedAt ?? product.CreatedAt);
        return new TorobProductDto
        {
            PageUnique = pageUnique,
            PageUrl = pageUrl,
            Title = Trim(title, 500),
            Subtitle = TrimOrNull(product.CategoryTitle, 500),
            ProductGroupId = product.Id.ToString("D"),
            CurrentPrice = ToRial(finalPrice, product.CurrencyType),
            OldPrice = basePrice > finalPrice ? ToRial(basePrice, product.CurrencyType) : null,
            Availability = availability,
            ImageLinks = ProductImages(product),
            Spec = BuildSpecifications(product.Features),
            CategoryName = TrimOrNull(product.CategoryTitle, 500),
            ShortDescription = TrimOrNull(product.ShortDescription, 3_000),
            DateAdded = ToIso8601(createdAt),
            DateUpdated = ToIso8601(updatedAt)
        };
    }

    private IReadOnlyList<string> ProductImages(CatalogProductSource product) =>
        new[] { product.ThumbnailImagePath }
            .Concat(product.Images)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => AbsoluteUrl(_mediaBaseUrl, path!))
            .Where(url => url.Length <= 1_500)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyDictionary<string, string> BuildSpecifications(IEnumerable<CatalogFeatureSource> features)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            var key = TrimOrNull(feature.Title, 200);
            var value = TrimOrNull(feature.Value, 1_000);
            if (key is null || value is null) continue;
            var candidate = key;
            for (var number = 2; result.ContainsKey(candidate); number++) candidate = Trim($"{key} {number}", 200);
            result[candidate] = value;
        }
        return result;
    }

    private static long ToRial(decimal price, byte currencyType)
    {
        var rial = currencyType == (byte)CurrencyType.Toman ? price * 10m : price;
        return (long)Math.Max(0m, Math.Round(rial, 0, MidpointRounding.AwayFromZero));
    }

    private static DateTime Max(DateTime left, DateTime right) => left >= right ? left : right;
    private static string ToIso8601(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToString("O");
    private static string Trim(string value, int max) => value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
    private static string? TrimOrNull(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Trim(value, max);
    private static string NormalizeBaseUrl(string? value, string fallback) => (string.IsNullOrWhiteSpace(value) ? fallback : value).TrimEnd('/');
    private static string AbsoluteUrl(string baseUrl, string path) => Uri.TryCreate(path, UriKind.Absolute, out var absolute)
        ? absolute.ToString()
        : $"{baseUrl}/{path.TrimStart('/')}";
    private static string NormalizeRequestedUrl(string value) => value.Trim().TrimEnd('/');

    private sealed class CatalogProductSource
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string? ShortDescription { get; init; }
        public string CategoryTitle { get; init; } = string.Empty;
        public byte DeliveryType { get; init; }
        public byte CurrencyType { get; init; }
        public decimal BasePrice { get; init; }
        public decimal? DiscountPrice { get; init; }
        public bool ForceOutOfStock { get; init; }
        public string? ThumbnailImagePath { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public List<string> Images { get; init; } = [];
        public List<CatalogFeatureSource> Features { get; init; } = [];
        public List<CatalogVariantSource> Variants { get; init; } = [];
    }

    private sealed class CatalogFeatureSource
    {
        public string Title { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }

    private sealed class CatalogVariantSource
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public decimal? DiscountPrice { get; init; }
        public byte StockMode { get; init; }
        public int StockQuantity { get; init; }
        public bool IsDefault { get; init; }
        public int SortOrder { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public int AvailableGiftCodes { get; init; }
    }
}
