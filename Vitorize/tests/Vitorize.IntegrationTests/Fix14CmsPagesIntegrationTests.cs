using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.DTOs.Seo;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-14 (Client Issue #1) over real HTTP and SQL Server: Admin CMS page management, system-page
/// protection, stored-XSS defence, publication gating and canonical sitemap URLs.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix14CmsPagesIntegrationTests
{
    private const string UnsafeHtml =
        "<h2>عنوان</h2><p onclick=\"alert(1)\">متن <strong>پررنگ</strong></p>" +
        "<script>window.__vzPwned=1</script><a href=\"javascript:alert(2)\">بد</a>" +
        "<ul><li>مورد</li></ul><iframe src=\"https://evil.test\"></iframe>";

    private readonly IntegrationTestFixture _fixture;

    public Fix14CmsPagesIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_customer_cannot_read_or_mutate_admin_pages()
    {
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(customerToken);

        foreach (var response in new[]
                 {
                     await client.GetAsync("/api/admin/pages"),
                     await client.PostAsJsonAsync("/api/admin/pages", NewPage("blocked-customer")),
                     await client.DeleteAsync($"/api/admin/pages/{Guid.NewGuid()}")
                 })
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_mutate_admin_pages()
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/pages", NewPage("blocked-anon"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_admin_creates_a_custom_page_whose_html_is_sanitized_on_save_and_on_read()
    {
        using var admin = await AdminClientAsync();
        var slug = UniqueSlug("story");

        var created = await CreateAsync(admin, new CreatePageRequestDto
        {
            Title = "داستان ما", Slug = slug, ContentHtml = UnsafeHtml,
            SeoTitle = "داستان ویتورایز", SeoDescription = "معرفی کوتاه", IsPublished = true
        });

        created.IsSystem.Should().BeFalse();
        created.Slug.Should().Be(slug);

        // Save-side sanitization: the persisted row is already clean.
        await using (var db = _fixture.CreateDbContext())
        {
            var stored = await db.Pages.AsNoTracking().SingleAsync(x => x.Slug == slug);
            stored.ContentHtml.Should().NotContain("script").And.NotContain("onclick")
                .And.NotContain("javascript:").And.NotContain("iframe");
            stored.ContentHtml.Should().Contain("<h2").And.Contain("<strong").And.Contain("<li");
        }

        // Read-side sanitization on the public projection.
        using var publicClient = _fixture.CreateClient();
        var page = await PublicPageAsync(publicClient, slug);
        page.ContentHtml.Should().NotContain("script").And.NotContain("onclick").And.NotContain("javascript:");
        page.ContentHtml.Should().Contain("<h2").And.Contain("<strong");
        page.SeoTitle.Should().Be("داستان ویتورایز");
        page.SeoDescription.Should().Be("معرفی کوتاه");
        page.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task Content_written_directly_to_the_database_is_still_sanitized_on_read()
    {
        // Defence in depth for legacy/imported rows that never passed through the Admin save path.
        var slug = UniqueSlug("legacy");
        await using (var db = _fixture.CreateDbContext())
        {
            db.Pages.Add(new Page
            {
                Id = Guid.NewGuid(), Title = "محتوای قدیمی", Slug = slug,
                ContentHtml = UnsafeHtml, IsPublished = true, IsSystem = false, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        var page = await PublicPageAsync(client, slug);

        page.ContentHtml.Should().NotContain("script").And.NotContain("onclick").And.NotContain("javascript:");
        page.ContentHtml.Should().Contain("<strong");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("checkout")]
    [InlineData("faq")]
    [InlineData("about")]
    [InlineData("contact")]
    public async Task A_reserved_or_system_slug_is_refused(string slug)
    {
        using var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/pages", NewPage(slug));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_duplicate_slug_is_refused_regardless_of_casing()
    {
        using var admin = await AdminClientAsync();
        var slug = UniqueSlug("dup");
        await CreateAsync(admin, NewPage(slug));

        var same = await admin.PostAsJsonAsync("/api/admin/pages", NewPage(slug));
        var upper = await admin.PostAsJsonAsync("/api/admin/pages", NewPage(slug.ToUpperInvariant()));

        same.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        upper.StatusCode.Should().Be(HttpStatusCode.BadRequest, "slug uniqueness is case-insensitive");
    }

    [Fact]
    public async Task Publication_gates_public_access_in_both_directions()
    {
        using var admin = await AdminClientAsync();
        using var publicClient = _fixture.CreateClient();
        var slug = UniqueSlug("gated");

        var created = await CreateAsync(admin, new CreatePageRequestDto
        {
            Title = "صفحه کنترل‌شده", Slug = slug, ContentHtml = "<p>محتوا</p>", IsPublished = false
        });

        // Unpublished must be indistinguishable from missing — no title or content leak.
        var hidden = await publicClient.GetAsync($"/api/pages/{slug}");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await hidden.Content.ReadAsStringAsync()).Should().NotContain("صفحه کنترل‌شده");

        (await admin.PostAsync($"/api/admin/pages/{created.Id}/publish", null)).EnsureSuccessStatusCode();
        (await PublicPageAsync(publicClient, slug)).Title.Should().Be("صفحه کنترل‌شده");

        (await admin.PostAsync($"/api/admin/pages/{created.Id}/unpublish", null)).EnsureSuccessStatusCode();
        (await publicClient.GetAsync($"/api/pages/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_slug_returns_not_found()
    {
        using var client = _fixture.CreateClient();

        (await client.GetAsync($"/api/pages/{UniqueSlug("ghost")}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_system_page_cannot_be_deleted_and_its_slug_cannot_be_renamed()
    {
        using var admin = await AdminClientAsync();
        var about = await SystemPageAsync(admin, PageSlugRules.System.About);

        var deleted = await admin.DeleteAsync($"/api/admin/pages/{about.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.BadRequest, await deleted.Content.ReadAsStringAsync());

        // A rename attempt succeeds as an edit but the slug identity is preserved.
        var renamed = await admin.PutAsJsonAsync($"/api/admin/pages/{about.Id}", new UpdatePageRequestDto
        {
            Title = "درباره ما - ویرایش‌شده", Slug = "hijacked-about",
            ContentHtml = "<p>معرفی</p>", IsPublished = false
        });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
        var updated = (await renamed.Content.ReadFromJsonAsync<ApiResult<AdminPageDto>>())!.Data!;
        updated.Slug.Should().Be(PageSlugRules.System.About);
        updated.Title.Should().Be("درباره ما - ویرایش‌شده");
        updated.IsSystem.Should().BeTrue();

        await using var db = _fixture.CreateDbContext();
        (await db.Pages.CountAsync(x => x.Slug == "hijacked-about")).Should().Be(0);
        (await db.Pages.AnyAsync(x => x.Id == about.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task A_custom_page_can_be_deleted()
    {
        using var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewPage(UniqueSlug("temp")));

        (await admin.DeleteAsync($"/api/admin/pages/{created.Id}")).EnsureSuccessStatusCode();

        await using var db = _fixture.CreateDbContext();
        (await db.Pages.AnyAsync(x => x.Id == created.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Publish_unpublish_and_delete_are_audited_without_logging_page_html()
    {
        using var admin = await AdminClientAsync();
        var before = DateTime.UtcNow.AddSeconds(-5);
        var created = await CreateAsync(admin, new CreatePageRequestDto
        {
            Title = "صفحه ممیزی", Slug = UniqueSlug("audit"),
            ContentHtml = "<p>SECRET-PAGE-BODY</p>", IsPublished = false
        });

        (await admin.PostAsync($"/api/admin/pages/{created.Id}/publish", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/pages/{created.Id}/unpublish", null)).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/admin/pages/{created.Id}")).EnsureSuccessStatusCode();

        await using var db = _fixture.CreateDbContext();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityName == "Page" && x.EntityId == created.Id.ToString() && x.CreatedAt >= before)
            .ToListAsync();

        logs.Select(x => x.ActionType).Should()
            .Contain("PagePublished").And.Contain("PageUnpublished").And.Contain("PageDeleted");
        logs.Should().OnlyContain(x => x.UserId != null);
        logs.Should().NotContain(x => x.Data != null && x.Data.Contains("SECRET-PAGE-BODY"),
            "audit records identity and action only, never page content");
    }

    [Fact]
    public async Task The_sitemap_lists_published_pages_once_at_their_canonical_url()
    {
        using var admin = await AdminClientAsync();
        using var publicClient = _fixture.CreateClient();
        var customSlug = UniqueSlug("map");

        var custom = await CreateAsync(admin, new CreatePageRequestDto
        {
            Title = "صفحه نقشه", Slug = customSlug, ContentHtml = "<p>x</p>", IsPublished = true
        });
        var about = await SystemPageAsync(admin, PageSlugRules.System.About);
        (await admin.PostAsync($"/api/admin/pages/{about.Id}/publish", null)).EnsureSuccessStatusCode();

        var published = await SitemapPathsAsync(publicClient);
        published.Should().Contain($"/page/{customSlug}", "custom pages stay under /page/{slug}");
        published.Should().Contain("/about", "system pages are canonical at their short route");
        published.Should().NotContain("/page/about", "one URL per page keeps the sitemap duplicate-free");

        (await admin.PostAsync($"/api/admin/pages/{custom.Id}/unpublish", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/pages/{about.Id}/unpublish", null)).EnsureSuccessStatusCode();

        var afterUnpublish = await SitemapPathsAsync(publicClient);
        afterUnpublish.Should().NotContain($"/page/{customSlug}");
        afterUnpublish.Should().NotContain("/about");
    }

    [Fact]
    public async Task The_four_system_pages_are_seeded_unpublished_exactly_once()
    {
        await using var db = _fixture.CreateDbContext();

        foreach (var slug in PageSlugRules.System.All)
        {
            var matches = await db.Pages.AsNoTracking().Where(x => x.Slug == slug).ToListAsync();
            matches.Should().ContainSingle($"the V0017 seed must be idempotent for '{slug}'");
            matches[0].IsSystem.Should().BeTrue();
            matches[0].Title.Should().NotBeNullOrWhiteSpace();
        }

        // The public storefront sees none of them until an administrator publishes.
        using var client = _fixture.CreateClient();
        foreach (var slug in PageSlugRules.System.All)
        {
            var seeded = await db.Pages.AsNoTracking().SingleAsync(x => x.Slug == slug);
            if (!seeded.IsPublished)
                (await client.GetAsync($"/api/pages/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    private async Task<HashSet<string>> SitemapPathsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/seo/sitemap/pages?page=1&pageSize=1000");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SitemapPageDto>>())!.Data!;
        return body.Items.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<AdminPageDto> SystemPageAsync(HttpClient admin, string slug)
    {
        var response = await admin.GetAsync("/api/admin/pages");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var all = (await response.Content.ReadFromJsonAsync<ApiResult<List<AdminPageListItemDto>>>())!.Data!;
        var item = all.SingleOrDefault(x => x.Slug == slug);
        item.Should().NotBeNull($"the V0017 seed must provide the '{slug}' system page");
        item!.IsSystem.Should().BeTrue();

        var detail = await admin.GetAsync($"/api/admin/pages/{item.Id}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await detail.Content.ReadFromJsonAsync<ApiResult<AdminPageDto>>())!.Data!;
    }

    private static async Task<AdminPageDto> CreateAsync(HttpClient admin, CreatePageRequestDto request)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/pages", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminPageDto>>())!.Data!;
    }

    private static async Task<PageDto> PublicPageAsync(HttpClient client, string slug)
    {
        var response = await client.GetAsync($"/api/pages/{slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<PageDto>>())!.Data!;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        return _fixture.CreateClient(token);
    }

    private static CreatePageRequestDto NewPage(string slug) => new()
    {
        Title = "صفحه آزمون", Slug = slug, ContentHtml = "<p>محتوا</p>", IsPublished = false
    };

    private static string UniqueSlug(string prefix) => $"fix14-{prefix}-{Guid.NewGuid():N}"[..24];
}
