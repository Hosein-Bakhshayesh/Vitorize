using System.Net;
using System.Text;
using System.Text.Json;
using Vitorize.Shared.Common;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

public sealed class SessionTokenRefreshCoordinatorTests
{
    [Fact]
    public async Task Parallel_requests_share_one_refresh_and_receive_the_rotated_tokens()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ApiResult<AdminLoginResponseModel>.Success(
            new AdminLoginResponseModel { AccessToken = "new-access", RefreshToken = "new-refresh" })));
        var coordinator = new SessionTokenRefreshCoordinator(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            coordinator.RefreshAsync(VitorizeAuthSchemes.CustomerScheme, "customer-refresh", CancellationToken.None)));

        Assert.Equal(1, handler.CallCount);
        Assert.All(results, x => { Assert.True(x.Success); Assert.Equal("new-access", x.AccessToken); Assert.Equal("new-refresh", x.RefreshToken); });
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Invalid_revoked_or_failed_refresh_never_returns_tokens(HttpStatusCode statusCode)
    {
        var coordinator = new SessionTokenRefreshCoordinator(new HttpClient(new StubHandler(_ => new HttpResponseMessage(statusCode))) { BaseAddress = new Uri("https://api.test/") });
        var result = await coordinator.RefreshAsync(VitorizeAuthSchemes.AdminScheme, "admin-refresh", CancellationToken.None);
        Assert.False(result.Success);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task Unknown_scheme_cannot_refresh_another_session()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("must not call API"));
        var coordinator = new SessionTokenRefreshCoordinator(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var result = await coordinator.RefreshAsync("wrong-scheme", "customer-refresh", CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int CallCount;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return Task.FromResult(_respond(request));
        }
    }
}
