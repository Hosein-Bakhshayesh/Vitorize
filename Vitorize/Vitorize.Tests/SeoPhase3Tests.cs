using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.Validators.Admin;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Web.Services;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests;

public sealed class SeoPhase3Tests
{
    [Fact]
    public void Canonical_uses_configured_https_origin_and_removes_query_fragment_and_trailing_slash()
    {
        var branding = new StoreBranding(new Dictionary<string, string>
        {
            ["Seo.CanonicalBaseUrl"] = "https://www.vitorize.com/ignored"
        });

        var canonical = SeoUrlBuilder.Canonical(
            branding,
            "http://localhost:5174/product/example?utm_source=test#buy");

        Assert.Equal("https://www.vitorize.com/product/example", canonical);
    }

    [Fact]
    public void JsonLd_escapes_script_termination_and_emits_schema_keys()
    {
        var json = SeoJsonLd.Breadcrumbs(("</script><script>alert(1)</script>", "https://vitorize.com/"));

        Assert.Contains("\"@context\":\"https://schema.org\"", json);
        Assert.Contains("\"@type\":\"BreadcrumbList\"", json);
        Assert.DoesNotContain("</script>", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\\u003C/script", json);
    }

    [Fact]
    public async Task Sitemap_only_contains_active_indexable_entities_and_uses_real_modified_date()
    {
        await using var db = Db();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Active", Slug = "active", IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var modified = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        db.Categories.Add(category);
        db.Products.AddRange(
            NewProduct(category.Id, "visible", true, modified),
            NewProduct(category.Id, "inactive", false, modified));
        await db.SaveChangesAsync();

        var page = await new SeoService(db).GetSitemapAsync("products", 1, 50_000);

        var item = Assert.Single(page.Items);
        Assert.Equal("/product/visible", item.Path);
        Assert.Equal(modified, item.LastModified);
    }

    [Fact]
    public async Task Redirect_resolution_is_exact_normalized_and_inactive_entries_are_ignored()
    {
        await using var db = Db();
        db.LegacyRedirects.AddRange(
            new LegacyRedirect { Id = Guid.NewGuid(), SourcePath = "/old-product", DestinationPath = "/product/new", StatusCode = 301, IsActive = true, CreatedAt = DateTime.UtcNow },
            new LegacyRedirect { Id = Guid.NewGuid(), SourcePath = "/retired", StatusCode = 410, IsActive = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new SeoService(db);
        var redirect = await service.ResolveRedirectAsync("old-product/?utm_source=legacy");

        Assert.NotNull(redirect);
        Assert.Equal("/product/new", redirect.DestinationPath);
        Assert.Equal(301, redirect.StatusCode);
        Assert.Null(await service.ResolveRedirectAsync("/retired"));
        Assert.Null(await service.ResolveRedirectAsync("/missing"));
    }

    [Fact]
    public void Product_tag_validator_limits_aliases_and_rejects_noncanonical_slugs()
    {
        var validator = new SaveProductTagRequestValidator();
        var tooManyAliases = string.Join(',', Enumerable.Range(1, 21).Select(x => $"alias{x}"));

        var result = validator.Validate(new SaveProductTagRequestDto
        {
            Title = "اکشن", Slug = "Invalid Slug", Aliases = tooManyAliases
        });

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SaveProductTagRequestDto.Slug));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SaveProductTagRequestDto.Aliases));
    }

    [Fact]
    public void Database_model_contains_phase3_columns_and_unique_redirect_source()
    {
        using var db = Db();
        var product = db.Model.FindEntityType(typeof(Product));
        var tag = db.Model.FindEntityType(typeof(ProductTag));
        var redirect = db.Model.FindEntityType(typeof(LegacyRedirect));

        Assert.NotNull(product?.FindProperty(nameof(Product.FocusKeyword)));
        Assert.NotNull(product?.FindProperty(nameof(Product.ThumbnailAltText)));
        Assert.NotNull(tag?.FindProperty(nameof(ProductTag.Aliases)));
        Assert.Contains(redirect!.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(LegacyRedirect.SourcePath)]));
    }

    private static VitorizeDbContext Db()
    {
        var options = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"seo-{Guid.NewGuid():N}")
            .Options;
        return new VitorizeDbContext(options);
    }

    private static Product NewProduct(Guid categoryId, string slug, bool active, DateTime modified) => new()
    {
        Id = Guid.NewGuid(), CategoryId = categoryId, Title = slug, Slug = slug,
        ProductType = 1, DeliveryType = 1, BasePrice = 100, CurrencyType = 1,
        MinOrderQuantity = 1, IsActive = active, CreatedAt = modified.AddDays(-1), UpdatedAt = modified
    };
}
