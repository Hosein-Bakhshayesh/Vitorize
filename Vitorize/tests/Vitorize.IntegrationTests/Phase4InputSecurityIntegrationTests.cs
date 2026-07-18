using System.Net;
using System.Text;
using FluentAssertions;
using Vitorize.IntegrationTests.Infrastructure;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Phase 4 input-security coverage (Part 6). Hostile inputs must be handled as safe client errors -
/// never a 500, never a leaked SQL error or stack trace, and never reflected as executable markup in
/// an HTML content type. A 500 here is a genuine defect, not a passing "it was rejected" outcome.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Phase4InputSecurityIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Phase4InputSecurityIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private static readonly string[] LeakSignatures =
    {
        "SqlException", "Microsoft.Data.SqlClient", "SQLSTATE", "Incorrect syntax near",
        "Unclosed quotation", "at Vitorize.", "StackTrace", "System.Data.SqlClient",
        "ConnectionString", "Server=", "Data Source=", "truncated",
    };

    public static IEnumerable<object[]> HostilePayloads() => new[]
    {
        new object[] { "' OR '1'='1" },
        new object[] { "'; DROP TABLE dbo.Products;--" },
        new object[] { "1' UNION SELECT NULL,NULL,NULL--" },
        new object[] { "\" OR 1=1 --" },
        new object[] { "<script>alert(1)</script>" },
        new object[] { "\"><img src=x onerror=alert(1)>" },
        new object[] { "javascript:alert(document.cookie)" },
        new object[] { "../../../../etc/passwd" },
        new object[] { "..%2f..%2fweb.config" },
        new object[] { "malware.jpg.exe" },
        new object[] { "‮gnp.exe" },
        new object[] { "😀🔥".PadRight(50, '☠') },
    };

    [Theory]
    [MemberData(nameof(HostilePayloads))]
    public async Task Public_search_is_resilient_to_hostile_input(string payload)
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/products?search={Uri.EscapeDataString(payload)}&page=1&pageSize=20");

        ((int)response.StatusCode).Should().BeLessThan(500, "hostile search input must not fault the server");
        var body = await response.Content.ReadAsStringAsync();
        AssertNoLeak(body);
        // A JSON API must not echo the payload back inside an HTML document that a browser would execute.
        (response.Content.Headers.ContentType?.MediaType ?? string.Empty).Should().NotBe("text/html");
    }

    [Theory]
    [MemberData(nameof(HostilePayloads))]
    public async Task Product_slug_lookup_rejects_hostile_input_without_server_error(string payload)
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/products/slug/{Uri.EscapeDataString(payload)}");

        ((int)response.StatusCode).Should().BeLessThan(500);
        AssertNoLeak(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000")]
    [InlineData("' OR 1=1--")]
    public async Task Invalid_guid_route_returns_client_error_not_server_error(string badId)
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/products/{Uri.EscapeDataString(badId)}");

        // The :guid route constraint should reject these as 404/400 - never a 500.
        ((int)response.StatusCode).Should().BeInRange(400, 404);
        AssertNoLeak(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Malformed_json_login_is_rejected_as_bad_request()
    {
        using var client = _fixture.CreateClient();
        var content = new StringContent("{ this is : not valid json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/login", content);

        ((int)response.StatusCode).Should().BeLessThan(500);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertNoLeak(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(101)]     // just past the narrowest searched column (ProductTag.Title, nvarchar(100))
    [InlineData(250)]     // production-reachable via a normal short URL - the real regression case
    [InlineData(10_000)]  // pathological
    public async Task Oversized_search_input_does_not_fault_the_server(int length)
    {
        using var client = _fixture.CreateClient();
        var huge = new string('a', length);
        var response = await client.GetAsync($"/api/products?search={huge}&page=1&pageSize=20");

        ((int)response.StatusCode).Should().BeLessThan(500,
            "an over-long search term must be capped, not raise a truncation SqlException");
        AssertNoLeak(await response.Content.ReadAsStringAsync());
    }

    private static void AssertNoLeak(string body)
    {
        foreach (var signature in LeakSignatures)
            body.Should().NotContain(signature, "hostile input must not leak database internals or stack traces");
    }
}
