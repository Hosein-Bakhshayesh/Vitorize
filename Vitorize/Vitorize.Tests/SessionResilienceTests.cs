using System.Net;
using System.Text;
using System.Text.Json;
using Vitorize.Shared.Common;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// A browser session must only end when something authoritative says it has ended.
///
/// The reported production symptom was customers being signed out mid-visit and having to clear their
/// browser data before the site worked again. The cause was not the token format or the key ring: it
/// was that every unsuccessful rotation - a timeout, a recycling API, a 502 from the proxy - was
/// treated as proof that the session was over. These tests pin the distinction that makes that
/// impossible: only the provider refusing the token itself is decisive.
/// </summary>
public sealed class SessionResilienceTests
{
    private static SessionTokenRefreshCoordinator Coordinator(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });

    private static HttpResponseMessage Rotated() =>
        Json(HttpStatusCode.OK, ApiResult<AdminLoginResponseModel>.Success(
            new AdminLoginResponseModel { AccessToken = "rotated-access", RefreshToken = "rotated-refresh" }));

    // ---------------------------------------------------------------- authoritative rejection

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_refused_refresh_token_is_reported_as_rejected(HttpStatusCode statusCode)
    {
        var result = await Coordinator(new StubHandler(_ => new HttpResponseMessage(statusCode)))
            .RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "dead-refresh-" + statusCode, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Rejected, result.Outcome);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task A_success_response_with_no_usable_pair_is_rejected_rather_than_retried_forever()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            ApiResult<AdminLoginResponseModel>.Success(new AdminLoginResponseModel())));

        var result = await Coordinator(handler).RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "empty-pair", CancellationToken.None);

        Assert.Equal(RefreshOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task An_unknown_scheme_is_rejected_and_never_reaches_the_provider()
    {
        var handler = new StubHandler(_ => Rotated());

        var result = await Coordinator(handler).RefreshAsync("SomeOtherScheme", "refresh", CancellationToken.None);

        Assert.Equal(RefreshOutcome.Rejected, result.Outcome);
        Assert.Equal(0, handler.CallCount);
    }

    // ---------------------------------------------------------------- transient failure

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_server_side_fault_is_transient_and_never_rejects_the_session(HttpStatusCode statusCode)
    {
        // These are exactly the codes a recycling app pool or a proxy emits. None of them says
        // anything about whether the customer's refresh token is still good.
        var result = await Coordinator(new StubHandler(_ => new HttpResponseMessage(statusCode)))
            .RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "live-refresh-" + statusCode, CancellationToken.None);

        Assert.Equal(RefreshOutcome.Transient, result.Outcome);
        Assert.NotEqual(RefreshOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task A_network_failure_is_transient()
    {
        var result = await Coordinator(new StubHandler(_ => throw new HttpRequestException("connection reset")))
            .RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "network-blip", CancellationToken.None);

        Assert.Equal(RefreshOutcome.Transient, result.Outcome);
    }

    [Fact]
    public async Task A_timeout_is_transient()
    {
        var result = await Coordinator(new StubHandler(_ => throw new TaskCanceledException("timed out")))
            .RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "timeout-case", CancellationToken.None);

        Assert.Equal(RefreshOutcome.Transient, result.Outcome);
    }

    // ---------------------------------------------------------------- single flight

    [Fact]
    public async Task Concurrent_requests_behind_one_expiry_share_a_single_rotation()
    {
        var handler = new StubHandler(_ => Rotated());
        var coordinator = Coordinator(handler);

        var results = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ =>
            coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "shared-refresh", CancellationToken.None)));

        Assert.Equal(1, handler.CallCount);
        Assert.All(results, x => Assert.Equal("rotated-refresh", x.RefreshToken));
    }

    [Fact]
    public async Task A_transient_failure_is_not_cached_so_the_next_request_can_recover()
    {
        // The behaviour that turns a one-second blip into a thirty-second outage if it is cached: the
        // second attempt must reach the provider again and succeed.
        var calls = 0;
        var handler = new StubHandler(_ =>
            Interlocked.Increment(ref calls) == 1
                ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                : Rotated());
        var coordinator = Coordinator(handler);

        var first = await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "recovering", CancellationToken.None);
        var second = await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "recovering", CancellationToken.None);

        Assert.Equal(RefreshOutcome.Transient, first.Outcome);
        Assert.Equal(RefreshOutcome.Success, second.Outcome);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task A_rejection_is_cached_so_a_dead_session_is_not_hammered()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var coordinator = Coordinator(handler);

        await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "settled-dead", CancellationToken.None);
        var second = await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "settled-dead", CancellationToken.None);

        Assert.Equal(RefreshOutcome.Rejected, second.Outcome);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Two_different_sessions_rotate_independently()
    {
        var handler = new StubHandler(_ => Rotated());
        var coordinator = Coordinator(handler);

        await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "customer-a", CancellationToken.None);
        await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "customer-b", CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task The_same_token_under_two_schemes_is_never_confused()
    {
        var handler = new StubHandler(_ => Rotated());
        var coordinator = Coordinator(handler);

        await coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "same-token-value", CancellationToken.None);
        await coordinator.RefreshAsync(VitorizeAuthSchemes.AdminScheme, "same-token-value", CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
    }

    // ---------------------------------------------------------------- helpers

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload) => new(status)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int _calls;
        public int CallCount => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(respond(request));
        }
    }
}
