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
        var client = CreateClient(protectedApi, refreshApi, new FakeTokenProvider(VitorizeAuthSchemes.CustomerScheme, ValidToken(TimeSpan.FromMinutes(20)), "client-post-refresh-3"));

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

    [Fact]
    public async Task Valid_post_does_not_refresh_before_the_mutation()
    {
        var protectedApi = new SequenceHandler(_ => Json(HttpStatusCode.OK, ApiResult<string>.Success("created")));
        var refreshApi = new SequenceHandler(_ => throw new InvalidOperationException("valid token must not refresh"));
        var client = CreateClient(protectedApi, refreshApi, new FakeTokenProvider(VitorizeAuthSchemes.AdminScheme, ValidToken(TimeSpan.FromMinutes(20)), "valid-post-refresh"));

        var result = await client.PostAsync<string>("admin/products", new { title = "x" });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, protectedApi.CallCount);
        Assert.Equal(0, refreshApi.CallCount);
    }

    [Fact]
    public async Task Near_expired_post_refreshes_before_sending_once_and_persists_rotated_tokens()
    {
        var protectedApi = new SequenceHandler(_ => Json(HttpStatusCode.OK, ApiResult<string>.Success("created")));
        var refreshApi = SuccessfulRefreshHandler();
        var persisted = new FakeTokenSessionPersistence();
        var client = CreateClient(protectedApi, refreshApi, new FakeTokenProvider(VitorizeAuthSchemes.AdminScheme, ValidToken(TimeSpan.FromSeconds(45)), "near-post-refresh"), persisted);

        var result = await client.PostAsync<string>("admin/products", new { title = "x" });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, refreshApi.CallCount);
        Assert.Equal(1, protectedApi.CallCount);
        Assert.Equal("Bearer new-access", protectedApi.AuthorizationValues.Single());
        Assert.Equal((VitorizeAuthSchemes.AdminScheme, "new-access", "new-refresh"), persisted.LastPersisted);
    }

    [Theory]
    [InlineData("put")]
    [InlineData("delete")]
    [InlineData("upload")]
    public async Task Expired_mutation_refreshes_before_sending(string operation)
    {
        var protectedApi = new SequenceHandler(_ => Json(HttpStatusCode.OK, ApiResult<string>.Success("ok")));
        var refreshApi = SuccessfulRefreshHandler();
        var client = CreateClient(protectedApi, refreshApi, new FakeTokenProvider(VitorizeAuthSchemes.CustomerScheme, ValidToken(TimeSpan.FromMinutes(-1)), $"expired-{operation}-refresh"));

        switch (operation)
        {
            case "put": await client.PutAsync<string>("profile", new { name = "x" }); break;
            case "delete": await client.DeleteAsync("cart/items/1"); break;
            case "upload":
                await using (var stream = new MemoryStream([1, 2, 3]))
                    await client.UploadAsync<string>("uploads", stream, "a.txt", "text/plain");
                break;
        }

        Assert.Equal(1, refreshApi.CallCount);
        Assert.Equal(1, protectedApi.CallCount);
        Assert.Equal("Bearer new-access", protectedApi.AuthorizationValues.Single());
    }

    [Fact]
    public async Task Failed_preflight_refresh_blocks_mutation_and_expires_the_local_session()
    {
        var protectedApi = new SequenceHandler(_ => throw new InvalidOperationException("mutation must not be sent"));
        var refreshApi = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var tokens = new FakeTokenProvider(VitorizeAuthSchemes.CustomerScheme, ValidToken(TimeSpan.FromMinutes(-1)), "failed-preflight-refresh");
        var client = CreateClient(protectedApi, refreshApi, tokens);

        var result = await client.PostAsync<string>("orders", new { productId = 1 });

        Assert.False(result.IsSuccess);
        Assert.Equal(0, protectedApi.CallCount);
        Assert.Equal(1, refreshApi.CallCount);
        Assert.True(tokens.WasCleared);
    }

    private static ApiClient CreateClient(SequenceHandler protectedApi, SequenceHandler refreshApi, FakeTokenProvider tokens, FakeTokenSessionPersistence? persistence = null) =>
        new(
            new HttpClient(protectedApi) { BaseAddress = new Uri("https://api.test/") },
            tokens,
            new SessionTokenRefreshCoordinator(new HttpClient(refreshApi) { BaseAddress = new Uri("https://api.test/") }),
            persistence ?? new FakeTokenSessionPersistence(),
            new EmptyServiceProvider(),
            null,
            NullLogger<ApiClient>.Instance);

    private static SequenceHandler SuccessfulRefreshHandler() => new(_ => Json(HttpStatusCode.OK,
        ApiResult<AdminLoginResponseModel>.Success(new AdminLoginResponseModel { AccessToken = "new-access", RefreshToken = "new-refresh" })));

    private static string ValidToken(TimeSpan expiresIn)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { exp = DateTimeOffset.UtcNow.Add(expiresIn).ToUnixTimeSeconds() })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"header.{payload}.signature";
    }

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

    private sealed class FakeTokenSessionPersistence : ITokenSessionPersistence
    {
        public (string Scheme, string AccessToken, string RefreshToken)? LastPersisted { get; private set; }
        public Task<bool> PersistAsync(string scheme, string accessToken, string refreshToken, CancellationToken cancellationToken)
        {
            LastPersisted = (scheme, accessToken, refreshToken);
            return Task.FromResult(true);
        }
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
