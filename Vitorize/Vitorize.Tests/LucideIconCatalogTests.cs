using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Categories;
using Vitorize.Application.DTOs.Products;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Icons;
using Xunit;

namespace Vitorize.Tests;

public sealed class LucideIconCatalogTests
{
    [Fact]
    public void Catalog_contains_full_pinned_package_and_expected_icons()
    {
        Assert.Equal("lucide-static", LucideIconCatalog.PackageName);
        Assert.Equal("1.24.0", LucideIconCatalog.Version);
        Assert.Equal(1746, LucideIconCatalog.Count);
        Assert.True(LucideIconCatalog.IsOfficialKey("gamepad-2"));
        Assert.True(LucideIconCatalog.IsOfficialKey("wallet-cards"));
        Assert.True(LucideIconCatalog.IsOfficialKey("brain-circuit"));
        Assert.All(LucideIconCatalog.Categories, category =>
            Assert.Contains(LucideIconCatalog.Entries, icon => icon.Category == category.Key));
    }

    [Fact]
    public void Every_existing_fixed_ui_icon_resolves_to_official_lucide_key()
    {
        var currentKeys = "activity alert arrow-down arrow-left arrow-right arrow-up bar-chart bell box calendar cart check check-circle chevron-down chevron-left chevron-right chevron-up clock copy credit-card dashboard dots download edit external eye file-text filter folder gamepad gift grid headphones heart home image inbox info key layers list lock log-in logout mail map-pin menu message package package-check percent phone plus refresh save search send settings shield shield-check shield-lock shopping-bag sliders star tag trash trending-up upload user user-check user-plus users wallet x x-circle zap"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(currentKeys, key => Assert.True(LucideIconCatalog.TryNormalizeKey(key, out _), $"Unmapped UI icon: {key}"));
        Assert.All(new[] { "star-off", "shapes", "mouse-pointer-click", "search-x", "history", "circle-question-mark", "funnel" },
            key => Assert.True(LucideIconCatalog.IsOfficialKey(key), $"Missing picker icon: {key}"));
        Assert.All(LucideIconCatalog.LegacyMappings, mapping => Assert.True(LucideIconCatalog.IsOfficialKey(mapping.Value), $"Invalid legacy target: {mapping.Key} -> {mapping.Value}"));
    }

    [Theory]
    [InlineData(" Gamepad-2 ", "gamepad-2")]
    [InlineData("WALLET_CARDS", "wallet-cards")]
    [InlineData("grid", "layout-grid")]
    [InlineData("cart", "shopping-cart")]
    [InlineData("logout", "log-out")]
    public void Keys_and_legacy_aliases_are_normalized(string raw, string expected)
    {
        Assert.True(LucideIconCatalog.TryNormalizeKey(raw, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Invalid_or_injectable_key_is_rejected_and_renderer_resolution_falls_back()
    {
        Assert.False(LucideIconCatalog.TryNormalizeKey("<svg onload=alert(1)>", out _));
        Assert.False(LucideIconCatalog.TryNormalizeKey("https://evil.example/icon.svg", out _));
        Assert.Equal("circle-question-mark", LucideIconCatalog.ResolveOrFallback("unknown-old-icon", "circle-question-mark"));
    }

    [Theory]
    [InlineData("کیف پول", "wallet")]
    [InlineData("كيف پول", "wallet")]
    [InlineData("هوش مصنوعی", "brain-circuit")]
    [InlineData("فضای ابری", "cloud")]
    [InlineData("تیکت", "ticket")]
    [InlineData("تلگرام", "send")]
    public void Persian_alias_search_returns_semantic_official_icon(string query, string expected)
    {
        Assert.Contains(LucideIconCatalog.Search(query).Take(8), x => x.Key == expected);
    }

    [Fact]
    public void English_partial_search_ranks_exact_and_prefix_matches_first()
    {
        Assert.Equal("wallet", LucideIconCatalog.Search("wallet").First().Key);
        Assert.StartsWith("gamepad", LucideIconCatalog.Search("gamep").First().Key);
        Assert.Contains(LucideIconCatalog.Search("controller").Take(8), x => x.Key == "gamepad-2");
    }

    [Fact]
    public void Category_filter_returns_only_requested_category()
    {
        var results = LucideIconCatalog.Search(null, "Gaming", 100);
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal("Gaming", x.Category));
    }

    [Fact]
    public void Central_rules_enforce_required_optional_and_normalized_storage()
    {
        Assert.Null(LucideIconRules.NormalizeOptional(null));
        Assert.Equal("shopping-cart", LucideIconRules.NormalizeRequired("cart"));
        Assert.Throws<BusinessException>(() => LucideIconRules.NormalizeRequired(null));
        Assert.Throws<BusinessException>(() => LucideIconRules.NormalizeOptional("<script>"));

        var feature = new ProductFeatureDto { Title = "نوع", Value = "دیجیتال", IconKey = "grid" };
        ProductFeatureRules.Validate(feature);
        Assert.Equal("layout-grid", feature.IconKey);
    }

    [Fact]
    public void Configurable_settings_json_normalizes_icons_and_rejects_unknown_keys()
    {
        var json = LucideIconRules.NormalizeConfigurableBlocksJson("[{\"icon\":\"grid\",\"title\":\"انتخاب\",\"text\":\"توضیح\"}]");
        using var document = JsonDocument.Parse(json);
        Assert.Equal("layout-grid", document.RootElement[0].GetProperty("icon").GetString());
        Assert.Throws<BusinessException>(() => LucideIconRules.NormalizeConfigurableBlocksJson("[{\"icon\":\"raw-svg\",\"title\":\"x\",\"text\":\"\"}]"));
    }

    [Fact]
    public async Task Category_service_persists_only_normalized_lucide_key()
    {
        await using var db = CreateDb();
        var service = new AdminCategoryService(db);
        var created = await service.CreateAsync(new CreateCategoryRequestDto
        {
            Title = "فروشگاه", Slug = $"shop-{Guid.NewGuid():N}", Icon = "cart", IsActive = true
        });

        Assert.Equal("shopping-cart", created.Icon);
        Assert.Equal("shopping-cart", await db.Categories.Where(x => x.Id == created.Id).Select(x => x.Icon).SingleAsync());
        await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(new CreateCategoryRequestDto
        {
            Title = "نامعتبر", Slug = $"invalid-{Guid.NewGuid():N}", Icon = "javascript:alert(1)"
        }));
    }

    private static VitorizeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new VitorizeDbContext(options);
    }
}
