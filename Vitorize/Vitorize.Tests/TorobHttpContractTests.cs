using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vitorize.Api.Controllers;
using Vitorize.Api.Filters;
using Vitorize.Api.Services;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Web.Endpoints;
using Xunit;

namespace Vitorize.Tests;

// Real loopback HTTP: storefront proxy -> MVC controller + lenient parser -> catalogue -> isolated data.
public sealed class TorobHttpContractTests(TorobHttpFixture fixture) : IClassFixture<TorobHttpFixture>
{
    private static readonly Regex TorobDate = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$", RegexOptions.CultureInvariant);

    // Torob's schema appendix, in its order. Optional fields are present with null, never omitted.
    private static readonly string[] ProductKeys =
    [
        "page_unique", "page_url", "title", "subtitle", "product_group_id", "current_price", "old_price",
        "availability", "image_links", "spec", "category_name", "short_desc", "guarantee", "date_added", "date_updated"
    ];

    [Theory]
    [InlineData("date_added_desc")]
    [InlineData("date_updated_desc")]
    public async Task Numbered_pages_have_100_offers_and_the_requested_order(string sort)
    {
        var ids = new List<string>();
        var dates = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            using var json = await PostOkAsync(new { page, sort });
            var root = json.RootElement;
            root.GetProperty("api_version").GetString().Should().Be("torob_api_v3");
            root.GetProperty("current_page").GetInt32().Should().Be(page);
            root.GetProperty("total").GetInt32().Should().Be(205);
            root.GetProperty("count").GetInt32().Should().Be(205);
            root.GetProperty("max_pages").GetInt32().Should().Be(3);
            var products = root.GetProperty("products").EnumerateArray().ToList();
            products.Should().HaveCount(page < 3 ? 100 : 5);
            foreach (var product in products)
            {
                ValidateProduct(product);
                ids.Add(product.GetProperty("page_unique").GetString()!);
                dates.Add(product.GetProperty(sort == "date_added_desc" ? "date_added" : "date_updated").GetString()!);
            }
        }
        ids.Should().OnlyHaveUniqueItems();
        dates.Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public async Task Page_without_sort_matches_explicit_date_added_sort_across_all_pages(string? sortJson)
    {
        for (var page = 1; page <= 3; page++)
        {
            var body = sortJson is null ? $"{{\"page\":{page}}}"
                : $"{{\"page\":{page},\"sort\":{sortJson}}}";
            using var actual = await fixture.PostAsync(body);
            actual.StatusCode.Should().Be(HttpStatusCode.OK);
            using var expected = await PostOkAsync(new { page, sort = "date_added_desc" });
            using var actualJson = JsonDocument.Parse(await actual.Content.ReadAsStringAsync());
            actualJson.RootElement.GetRawText().Should().Be(expected.RootElement.GetRawText());
        }
    }

    [Fact]
    public async Task Cursor_traversal_returns_every_offer_once_and_null_at_the_end()
    {
        string? cursor = null;
        var ids = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            object request = cursor is null ? new { sort = "product_id_desc" }
                : new { sort = "product_id_desc", cursor };
            using var json = await PostOkAsync(request);
            var root = json.RootElement;
            root.GetProperty("current_page").GetInt32().Should().Be(page);
            var products = root.GetProperty("products").EnumerateArray().ToList();
            products.Should().HaveCount(page < 3 ? 100 : 5);
            ids.AddRange(products.Select(product => product.GetProperty("page_unique").GetString()!));
            cursor = root.GetProperty("next_cursor").GetString();
            if (page < 3) cursor.Should().NotBeNullOrEmpty();
            else cursor.Should().BeNull();
        }
        ids.Should().HaveCount(205).And.OnlyHaveUniqueItems();
        ids.Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Url_and_id_lookups_return_the_same_offer_and_skip_deleted_offers()
    {
        using var page = await PostOkAsync(new { page = 1, sort = "date_added_desc" });
        var offer = page.RootElement.GetProperty("products")[0];
        using var byId = await PostOkAsync(new { page_uniques = new[] { offer.GetProperty("page_unique").GetString(), "deleted" } });
        using var byUrl = await PostOkAsync(new { page_urls = new[] { offer.GetProperty("page_url").GetString(), "https://vitorize.example/product/deleted" } });
        byId.RootElement.GetProperty("products").GetArrayLength().Should().Be(1);
        byId.RootElement.GetProperty("products").GetRawText().Should().Be(byUrl.RootElement.GetProperty("products").GetRawText());
        using var deleted = await PostOkAsync(new { page_uniques = new[] { "deleted" } });
        deleted.RootElement.GetProperty("products").GetArrayLength().Should().Be(0);
        deleted.RootElement.GetProperty("total").GetInt32().Should().Be(0);
        deleted.RootElement.GetProperty("max_pages").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Explicit_lookups_are_not_limited_to_100_or_64_kilobytes()
    {
        var urls = Enumerable.Range(0, 110).Select(i => $"https://vitorize.example/product/{i}/{new string('x', 650)}").ToList();
        using var json = await PostOkAsync(new { page_urls = urls });
        json.RootElement.GetProperty("products").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Response_matches_the_torob_appendix_shape()
    {
        using var json = await PostOkAsync(new { page = 1, sort = "date_added_desc" });
        var root = json.RootElement;

        root.EnumerateObject().Select(property => property.Name)
            .Should().Equal("api_version", "current_page", "total", "count", "max_pages", "next_cursor", "products");
        root.GetProperty("next_cursor").ValueKind.Should().Be(JsonValueKind.Null);

        var first = root.GetProperty("products")[0];
        first.GetProperty("title").GetString().Should().Be("Offer 205");
        // 205 * 100 = 20,500 Rial in the database -> 2,050 Toman on Torob.
        first.GetProperty("current_price").GetInt64().Should().Be(2050);
        first.GetProperty("old_price").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("guarantee").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("date_added").GetString().Should().Be("1970-07-25T00:00:00+00:00");
        first.GetProperty("page_url").GetString().Should().Be("https://vitorize.com/product/offer-205");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("text")]
    [InlineData("{}")]
    [InlineData("{\"page\":1,\"sort\":{}}")]
    [InlineData("{\"page\":0,\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":\"abc\",\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":1.5,\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":true,\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":1,\"sort\":\"unknown\"}")]
    [InlineData("{\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":1,\"sort\":\"date_added_desc\",\"page_urls\":[]}")]
    [InlineData("{\"page_urls\":[]}")]
    [InlineData("{\"page_uniques\":[]}")]
    [InlineData("{\"page_uniques\":[null]}")]
    [InlineData("{\"page_uniques\":[true]}")]
    [InlineData("{\"sort\":\"product_id_desc\",\"cursor\":\"invalid\"}")]
    [InlineData("{\"sort\":\"product_id_desc\",\"cursor\":42}")]
    [InlineData("{\"cursor\":\"invalid\"}")]
    public async Task Invalid_requests_return_400_with_only_the_documented_error_field(string body)
    {
        using var response = await fixture.PostAsync(body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("error");
        json.RootElement.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("{\"page\":\"1\",\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":1.0,\"sort\":\"date_added_desc\"}")]
    [InlineData("{\"page\":1,\"sort\":\"DATE_ADDED_DESC\"}")]
    [InlineData("{\"page\":1,\"sort\":\"date_added_desc\",\"limit\":100,\"size\":100}")]
    [InlineData("{\"page\":1,\"sort\":\"date_added_desc\",\"cursor\":null,\"page_urls\":null}")]
    [InlineData("{\"page_uniques\":[42]}")]
    [InlineData("{\"page_uniques\":[\"one\"],\"sort\":\"\"}")]
    [InlineData("{\"page_uniques\":[\"one\"],\"sort\":null,\"cursor\":null}")]
    [InlineData("{\"page_urls\":\"https://vitorize.com/product/offer-1\"}")]
    [InlineData("{\"sort\":\"product_id_desc\",\"page\":1}")]
    [InlineData("{\"sort\":\"PRODUCT_ID_DESC\",\"limit\":100}")]
    [InlineData("{\"sort\":\"product_id_desc\",\"size\":100}")]
    public async Task Lenient_bodies_are_accepted(string body)
    {
        using var response = await fixture.PostAsync(body);
        using var json = await ReadOkAsync(response);
        json.RootElement.GetProperty("api_version").GetString().Should().Be("torob_api_v3");
    }

    [Fact]
    public async Task Non_json_content_types_are_accepted()
    {
        const string json = "{\"page\":1,\"sort\":\"date_added_desc\"}";
        const string offerUrl = "https://vitorize.com/product/offer-1";

        await ExpectFirstPage(new FormUrlEncodedContent([new("page", "1"), new("sort", "date_added_desc")]));
        await ExpectFirstPage(new MultipartFormDataContent
        {
            { new StringContent("1"), "page" },
            { new StringContent("date_added_desc"), "sort" }
        });
        await ExpectFirstPage(new StringContent(json, Encoding.UTF8, "text/plain"));
        await ExpectFirstPage(new StringContent("page=1&sort=date_added_desc", Encoding.UTF8, "text/plain"));

        var untyped = new StringContent(json, Encoding.UTF8);
        untyped.Headers.ContentType = null;
        await ExpectFirstPage(untyped);

        var withBom = new ByteArrayContent([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(json)]);
        withBom.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        await ExpectFirstPage(withBom);

        using var byForm = await fixture.PostAsync(new FormUrlEncodedContent(
            [new("page_urls[]", offerUrl), new("page_urls[]", "https://vitorize.com/product/missing")]));
        using var lookup = await ReadOkAsync(byForm);
        lookup.RootElement.GetProperty("products").GetArrayLength().Should().Be(1);
        lookup.RootElement.GetProperty("products")[0].GetProperty("page_url").GetString().Should().Be(offerUrl);

        using var badPage = await fixture.PostAsync(new FormUrlEncodedContent([new("page", "abc"), new("sort", "date_added_desc")]));
        await ExpectDocumentedError(badPage);
        await ExpectFirstPage(new FormUrlEncodedContent([new("page", "1")]));

        async Task ExpectFirstPage(HttpContent content)
        {
            using var response = await fixture.PostAsync(content);
            using var document = await ReadOkAsync(response);
            document.RootElement.GetProperty("current_page").GetInt32().Should().Be(1);
            document.RootElement.GetProperty("products").GetArrayLength().Should().Be(100);
        }

        static async Task ExpectDocumentedError(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("error");
        }
    }

    [Fact]
    public async Task Missing_token_is_accepted_while_enforcement_is_off()
    {
        using var response = await fixture.PostAsync(
            new StringContent("{\"page\":1,\"sort\":\"date_added_desc\"}", Encoding.UTF8, "application/json"),
            includeToken: false);
        using var json = await ReadOkAsync(response);
        json.RootElement.GetProperty("products").GetArrayLength().Should().Be(100);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("C-Torob-Token-Version")]
    [InlineData("X-Torob-Token-Version")]
    public async Task Version_header_is_optional_and_proxy_forwards_request_headers(string? versionHeaderName)
    {
        using var response = await fixture.PostAsync(
            new StringContent("{\"page\":1,\"sort\":\"date_added_desc\"}", Encoding.UTF8, "application/json"),
            versionHeaderName: versionHeaderName,
            version: "1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.LastUserAgent.Should().Be("torob.com");
        fixture.LastAccept.Should().Be("application/json");
        fixture.LastContentLength.Should().BeGreaterThan(0);
        fixture.LastForwardedFor.Should().Be("127.0.0.1");
        fixture.LastForwardedHost.Should().Be(fixture.WebHost);
        if (versionHeaderName is not null) fixture.LastHeaderNames.Should().Contain(versionHeaderName);
    }

    private async Task<JsonDocument> PostOkAsync(object body)
    {
        using var response = await fixture.PostAsync(JsonSerializer.Serialize(body));
        return await ReadOkAsync(response);
    }

    private static async Task<JsonDocument> ReadOkAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        return JsonDocument.Parse(content);
    }

    private static void ValidateProduct(JsonElement product)
    {
        product.EnumerateObject().Select(property => property.Name).Should().Equal(ProductKeys);
        product.GetProperty("page_unique").GetString()!.Length.Should().BeInRange(1, 200);
        product.GetProperty("title").GetString()!.Length.Should().BeInRange(1, 500);
        product.GetProperty("category_name").GetString()!.Length.Should().BeLessThanOrEqualTo(200);
        product.GetProperty("short_desc").GetString()!.Length.Should().BeLessThanOrEqualTo(500);
        product.GetProperty("current_price").TryGetInt64(out _).Should().BeTrue();
        product.GetProperty("old_price").ValueKind.Should().BeOneOf(JsonValueKind.Null, JsonValueKind.Number);
        product.GetProperty("guarantee").ValueKind.Should().BeOneOf(JsonValueKind.Null, JsonValueKind.String);
        product.GetProperty("availability").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        product.GetProperty("spec").ValueKind.Should().Be(JsonValueKind.Object);
        Uri.IsWellFormedUriString(product.GetProperty("page_url").GetString(), UriKind.Absolute).Should().BeTrue();
        foreach (var image in product.GetProperty("image_links").EnumerateArray())
        {
            image.GetString()!.Length.Should().BeLessThanOrEqualTo(1000);
            Uri.IsWellFormedUriString(image.GetString(), UriKind.Absolute).Should().BeTrue();
        }
        product.GetProperty("date_added").GetString().Should().MatchRegex(TorobDate);
        product.GetProperty("date_updated").GetString().Should().MatchRegex(TorobDate);
    }
}

public sealed class TorobHttpFixture : IAsyncLifetime
{
    private WebApplication _api = null!;
    private WebApplication _web = null!;
    private HttpClient _client = null!;
    public string? LastUserAgent { get; private set; }
    public string? LastAccept { get; private set; }
    public long? LastContentLength { get; private set; }
    public string? LastForwardedFor { get; private set; }
    public string? LastForwardedHost { get; private set; }
    public string[] LastHeaderNames { get; private set; } = [];
    public string WebHost { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddApplicationPart(typeof(TorobController).Assembly);
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ITorobRequestAuthenticator, TorobRequestAuthenticator>();
        builder.Services.AddSingleton<ITorobRequestParser, TorobRequestParser>();
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<VitorizeDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddScoped<ITorobCatalogService, TorobCatalogService>();
        _api = builder.Build();
        _api.Use(async (context, next) =>
        {
            LastUserAgent = context.Request.Headers.UserAgent.ToString();
            LastAccept = context.Request.Headers.Accept.ToString();
            LastContentLength = context.Request.ContentLength;
            LastForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            LastForwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
            LastHeaderNames = context.Request.Headers.Keys.ToArray();
            await next(context);
        });
        _api.UseAuthorization();
        _api.MapControllers();
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();
            var category = new Category { Id = Guid.NewGuid(), Title = new string('c', 250), Slug = "test", IsActive = true };
            db.Add(category);
            for (var i = 1; i <= 205; i++)
            {
                var product = new Product
                {
                    Id = Guid.Parse(i.ToString("x32")), CategoryId = category.Id, Category = category,
                    Title = $"Offer {i}", Slug = $"offer-{i}", IsActive = true, BasePrice = i * 100,
                    CurrencyType = (byte)CurrencyType.Rial, DeliveryType = (byte)DeliveryType.Manual,
                    ThumbnailImagePath = "uploads/test.png", ShortDescription = new string('s', 700),
                    CreatedAt = DateTime.UnixEpoch.AddDays(i), UpdatedAt = DateTime.UnixEpoch.AddDays(1000 - i)
                };
                db.Add(product);
            }
            await db.SaveChangesAsync();
        }
        await _api.StartAsync();

        var webBuilder = WebApplication.CreateBuilder();
        webBuilder.Logging.ClearProviders();
        webBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        webBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiSettings:BaseUrl"] = Address(_api) + "/api" });
        webBuilder.Services.AddHttpClient("TorobProxy");
        webBuilder.Services.AddAuthorization();
        webBuilder.Services.AddAntiforgery();
        _web = webBuilder.Build();
        _web.UseAuthorization();
        _web.UseAntiforgery();
        _web.MapTorobEndpoints();
        await _web.StartAsync();
        WebHost = new Uri(Address(_web)).Authority;
        _client = new HttpClient { BaseAddress = new Uri(Address(_web)), Timeout = TimeSpan.FromSeconds(20) };
    }

    public Task<HttpResponseMessage> PostAsync(string body, string? version = null) =>
        PostAsync(
            new StringContent(body, Encoding.UTF8, "application/json"),
            versionHeaderName: version is null ? null : "C-Torob-Token-Version",
            version: version);

    public async Task<HttpResponseMessage> PostAsync(
        HttpContent content,
        bool includeToken = true,
        string? versionHeaderName = null,
        string? version = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/thirdparties/torob/products") { Content = content };
        if (includeToken) request.Headers.Add("X-Torob-Token", "non-secret-test-value");
        if (versionHeaderName is not null && version is not null) request.Headers.Add(versionHeaderName, version);
        request.Headers.Add("User-Agent", "torob.com");
        request.Headers.Add("Accept", "application/json");
        return await _client.SendAsync(request);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_web is not null) await _web.DisposeAsync();
        if (_api is not null) await _api.DisposeAsync();
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
}
