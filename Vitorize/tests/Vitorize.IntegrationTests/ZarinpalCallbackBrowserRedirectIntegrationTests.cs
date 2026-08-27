using System.Net;
using FluentAssertions;
using Vitorize.IntegrationTests.Infrastructure;

namespace Vitorize.IntegrationTests;

/// <summary>
/// The Zarinpal callback receives the SHOPPER'S BROWSER, so a browser navigation must end on the
/// storefront's result page - a real customer once landed on a raw JSON document after paying.
/// Non-browser clients keep the JSON contract these tests' siblings rely on.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class ZarinpalCallbackBrowserRedirectIntegrationTests
{
    private const string UnknownAuthority = "A00000000000000000000000000000000000";

    private readonly IntegrationTestFixture _fixture;
    public ZarinpalCallbackBrowserRedirectIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_browser_navigation_is_redirected_to_the_storefront_result_page()
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/payments/zarinpal/callback?Authority={UnknownAuthority}&Status=OK");
        // The Accept header a real browser sends on a top-level navigation.
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "a shopper must never be left staring at a JSON document");
        response.Headers.Location!.ToString().Should().Be("http://localhost:5077/payment/result?paid=0",
            "an unverifiable authority is a failed payment, presented on the branded result page");
    }

    [Fact]
    public async Task A_non_browser_client_keeps_the_json_contract()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/payments/zarinpal/callback?Authority={UnknownAuthority}&Status=OK");

        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect,
            "server-to-server consumers negotiate JSON and must keep receiving it");
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"", "the body stays machine-readable JSON");
    }
}
