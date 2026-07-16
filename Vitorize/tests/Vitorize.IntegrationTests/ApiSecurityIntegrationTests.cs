using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Logging;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class ApiSecurityIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public ApiSecurityIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/api/cart")]
    [InlineData("/api/wallet")]
    [InlineData("/api/notifications")]
    [InlineData("/api/tickets")]
    [InlineData("/api/verification/me")]
    [InlineData("/api/admin/settings")]
    public async Task Protected_endpoints_reject_anonymous_requests(string path)
    {
        using var client = _fixture.CreateClient();
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/admin/settings")]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/payments")]
    [InlineData("/api/admin/security-logs")]
    [InlineData("/api/health/details")]
    public async Task Customer_tokens_cannot_cross_admin_or_diagnostics_boundaries(string path)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Minimal_health_endpoint_is_public_but_detailed_health_is_protected()
    {
        using var client = _fixture.CreateClient();
        var health = await client.GetAsync("/api/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).Should().Be("{\"status\":\"Healthy\"}");
        (await client.GetAsync("/api/health/details")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Security_headers_and_correlation_id_are_present_on_API_responses()
    {
        using var client = _fixture.CreateClient();
        const string correlation = "integration-correlation-123";
        client.DefaultRequestHeaders.Add(CorrelationIdPolicy.HeaderName, correlation);
        var response = await client.GetAsync("/api/health");

        response.Headers.GetValues(CorrelationIdPolicy.HeaderName).Should().ContainSingle(correlation);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle();
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle();
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();
    }

    [Fact]
    public async Task Invalid_correlation_id_is_replaced_instead_of_reflected()
    {
        using var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationIdPolicy.HeaderName, new string('x', 80));
        var response = await client.GetAsync("/api/health");
        var returned = response.Headers.GetValues(CorrelationIdPolicy.HeaderName).Single();
        returned.Should().NotBe(new string('x', 80));
        Guid.TryParse(returned, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Private_verification_media_is_not_served_by_static_file_middleware()
    {
        using var client = _fixture.CreateClient();
        (await client.GetAsync("/uploads/verifications/secret-document.jpg")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Public_settings_response_excludes_SMS_API_key_and_encryption_secrets()
    {
        await using (var db = _fixture.CreateDbContext())
        {
            var smsKey = await db.Settings.SingleAsync(x => x.Key == "Sms.ApiKey");
            smsKey.Value = "integration-secret-sms-key";
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        var body = await (await client.GetAsync("/api/settings/public")).Content.ReadAsStringAsync();
        body.Should().NotContain("integration-secret-sms-key")
            .And.NotContain("Sms.ApiKey")
            .And.NotContain("Encryption:Key")
            .And.NotContain("Jwt:SecretKey");
    }
}
