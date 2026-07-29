using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Shared.Common;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

public sealed class ApiClientRefreshTests
{
    [Fact]
    public async Task Get_401_refreshes_once_and_retries_the_original_request_once()
    {
        var protectedApi = new SequenceHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => Json(HttpStatusCode.OK, ApiResult<string>.Success("retried")));
        var refreshApi = new SequenceHandler(_ => Json(HttpStatusCode.OK,
            ApiResult<AdminLoginResponseModel>.Success(new AdminLoginResponseModel { AccessToken = "new-access", RefreshToken = "new-refresh" })));
        var tokens = new FakeTokenProvider(VitorizeAuthSchemes.CustomerScheme, "old-access", "client-retry-refresh-1");
        var client = CreateClient(protectedApi, refreshApi, tokens);

        var result = await client.GetAsync<string>("protected");

        Assert.True(result.IsSuccess);
        Assert.Equal("retried", result.Data);
        Assert.Equal(2, protectedApi.CallCount);
        Assert.Equal(1, refreshApi.CallCount);
        Assert.Equal(["Bearer old-access", "Bearer new-access"], protectedApi.AuthorizationValues);
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, tokens.Scheme);
        Assert.Equal("new-access", tokens.AccessToken);
    }

    [Fact]
    public async Task Repeated_401_is_not_retried_in_a_loop()
    {
        var protectedApi = new SequenceHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var refreshApi = new SequenceHandler(_ => Json(HttpStatusCode.OK,
            ApiResult<AdminLoginResponseModel>.Success(new AdminLoginResponseModel { AccessToken = "new-access", RefreshToken = "new-refresh" })));
        var client = CreateClient(protectedApi, refreshApi, new FakeTokenProvider(VitorizeAuthSchemes.AdminScheme, "old-access", "client-retry-refresh-2"));

        var result = await client.GetAsync<string>("protected");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, protectedApi.CallCount);
        Assert.Equal(1, refreshApi.CallCount);
    }

    [Fact]
    public async Task Post_401_is_not_replayed_or_refreshed()
    {
        var protectedApi = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var refreshApi = new SequenceHandler(_ => throw new InvalidOperationException("unsafe POST must not refresh"));
        var client = CreateClient(protectedApi, refreshApi, new FakeTokenProvider(VitorizeAuthSchemes.CustomerScheme, "old-access", "client-post-refresh-3"));

        await client.PostAsync<string>("orders", new { productId = 1 });

        Assert.Equal(1, protectedApi.CallCount);
        Assert.Equal(0, refreshApi.CallCount);
    }

    [Fact]
    public async Task Failed_refresh_clears_only_the_affected_local_scheme()
    {
        var protectedApi = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var refreshApi = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var tokens = new FakeTokenProvider(VitorizeAuthSchemes.CustomerScheme, "old-access", "client-failed-refresh-4");
        var client = CreateClient(protectedApi, refreshApi, tokens);

        await client.GetAsync<string>("protected");

        Assert.Equal(1, protectedApi.CallCount);
        Assert.Equal(1, refreshApi.CallCount);
        Assert.True(tokens.WasCleared);
        Assert.Null(tokens.AccessToken);
        Assert.Null(tokens.RefreshToken);
    }

    private static ApiClient CreateClient(SequenceHandler protectedApi, SequenceHandler refreshApi, FakeTokenProvider tokens) =>
        new(
            new HttpClient(protectedApi) { BaseAddress = new Uri("https://api.test/") },
            tokens,
            new SessionTokenRefreshCoordinator(new HttpClient(refreshApi) { BaseAddress = new Uri("https://api.test/") }),
            new EmptyServiceProvider(),
            null,
            NullLogger<ApiClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class FakeTokenProvider(string scheme, string accessToken, string refreshToken) : IAccessTokenProvider
    {
        public string? Scheme { get; private set; } = scheme;
        public string? AccessToken { get; private set; } = accessToken;
        public string? RefreshToken { get; private set; } = refreshToken;
        public bool WasCleared { get; private set; }
        public Task<string?> GetAccessTokenAsync() => Task.FromResult(AccessToken);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(RefreshToken);
        public Task<string?> GetSchemeAsync() => Task.FromResult(Scheme);
        public void SetTokens(string tokenScheme, string tokenAccess, string tokenRefresh) =>
            (Scheme, AccessToken, RefreshToken) = (tokenScheme, tokenAccess, tokenRefresh);
        public void ClearTokens()
        {
            WasCleared = true;
            (Scheme, AccessToken, RefreshToken) = (null, null, null);
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class SequenceHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);
        public int CallCount { get; private set; }
        public List<string?> AuthorizationValues { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            AuthorizationValues.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }
}
