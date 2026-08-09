using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix01BTokenRotationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix01BTokenRotationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Order_mutation_after_expiry_refreshes_once_before_the_real_admin_mutation()
    {
        var session = await CreateAdminSessionAsync();
        var order = await CreatePendingOrderAsync();
        var recording = new RequestRecording();
        var tokens = new InMemoryTokens(VitorizeAuthSchemes.AdminScheme, ExpiredAccessToken(), session.RefreshToken);
        var persistence = new RecordingPersistence();
        var client = CreateApiClient(tokens, persistence, recording);

        var result = await client.PostAsync($"admin/orders/{order.Id}/cancel", new CancelOrderRequestDto { Reason = "FIX-01B test" });

        result.IsSuccess.Should().BeTrue();
        recording.RefreshRequests.Should().Be(1);
        recording.MutationRequests.Should().Be(1);
        recording.Events.Select(x => x.Kind).Should().ContainInOrder("refresh", "mutation");
        recording.MutationAuthorizationFingerprints.Should().ContainSingle()
            .Which.Should().Be(persistence.AccessFingerprint);
        recording.MutationAuthorizationFingerprints.Should().NotContain(Fingerprint(tokens.InitialAccessToken));
        persistence.Persisted.Should().BeTrue();
        tokens.AccessToken.Should().NotBe(tokens.InitialAccessToken);
        tokens.RefreshToken.Should().NotBe(session.RefreshToken);

        await using var db = _fixture.CreateDbContext();
        (await db.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.Cancelled);
        (await db.OrderStatusHistories.CountAsync(x => x.OrderId == order.Id)).Should().Be(1);
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == session.User.Id && x.RevocationReason == "Rotated"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Revoked_refresh_token_blocks_mutation_and_ends_the_local_session()
    {
        var session = await CreateAdminSessionAsync();
        var order = await CreatePendingOrderAsync();
        await RevokeRefreshTokenAsync(session.AccessToken, session.RefreshToken);
        var recording = new RequestRecording();
        var tokens = new InMemoryTokens(VitorizeAuthSchemes.AdminScheme, ExpiredAccessToken(), session.RefreshToken);
        var persistence = new RecordingPersistence();
        var client = CreateApiClient(tokens, persistence, recording);

        var result = await client.PostAsync($"admin/orders/{order.Id}/cancel", new CancelOrderRequestDto { Reason = "must not reach API" });

        result.IsSuccess.Should().BeFalse();
        recording.RefreshRequests.Should().BeLessOrEqualTo(1);
        recording.MutationRequests.Should().Be(0);
        recording.Events.Should().OnlyContain(x => x.Kind == "refresh");
        tokens.WasCleared.Should().BeTrue();
        tokens.AccessToken.Should().BeNull();
        tokens.RefreshToken.Should().BeNull();
        persistence.Persisted.Should().BeFalse();

        await using var db = _fixture.CreateDbContext();
        (await db.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.PendingPayment);
    }

    [Fact]
    public async Task Concurrent_real_requests_share_one_refresh_rotation_and_do_not_replay_the_mutation()
    {
        var session = await CreateAdminSessionAsync();
        var order = await CreatePendingOrderAsync();
        var recording = new RequestRecording();
        var coordinator = CreateCoordinator(recording);
        var clients = Enumerable.Range(0, 20)
            .Select(_ =>
            {
                var tokens = new InMemoryTokens(VitorizeAuthSchemes.AdminScheme, ExpiredAccessToken(), session.RefreshToken);
                return (Client: CreateApiClient(tokens, new RecordingPersistence(), recording, coordinator), Tokens: tokens);
            })
            .ToArray();

        var requests = clients.Take(19)
            .Select(x => x.Client.GetAsync<string>("admin/orders"))
            .Cast<Task>()
            .Append(clients[^1].Client.PostAsync($"admin/orders/{order.Id}/cancel", new CancelOrderRequestDto { Reason = "concurrent FIX-01B test" }))
            .ToArray();
        await Task.WhenAll(requests);

        recording.RefreshRequests.Should().Be(1);
        recording.MutationRequests.Should().Be(1);
        recording.MutationAuthorizationFingerprints.Should().ContainSingle();
        clients.Should().OnlyContain(x => x.Tokens.AccessToken != x.Tokens.InitialAccessToken);
        clients.Should().OnlyContain(x => x.Tokens.RefreshToken != session.RefreshToken);

        await using var db = _fixture.CreateDbContext();
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == session.User.Id && x.RevocationReason == "Rotated"))
            .Should().Be(1);
        (await db.OrderStatusHistories.CountAsync(x => x.OrderId == order.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Rotated_tokens_are_authoritative_in_a_new_api_client_scope()
    {
        var session = await CreateAdminSessionAsync();
        var order = await CreatePendingOrderAsync();
        var firstRecording = new RequestRecording();
        var firstTokens = new InMemoryTokens(VitorizeAuthSchemes.AdminScheme, ExpiredAccessToken(), session.RefreshToken);
        var persistence = new RecordingPersistence();
        var firstClient = CreateApiClient(firstTokens, persistence, firstRecording);

        (await firstClient.PostAsync($"admin/orders/{order.Id}/cancel", new CancelOrderRequestDto { Reason = "persistence FIX-01B test" }))
            .IsSuccess.Should().BeTrue();
        persistence.Persisted.Should().BeTrue();

        var rereadTokens = new InMemoryTokens(VitorizeAuthSchemes.AdminScheme, persistence.AccessToken!, persistence.RefreshToken!);
        var secondRecording = new RequestRecording();
        var secondClient = CreateApiClient(rereadTokens, new RecordingPersistence(), secondRecording);
        var secondResult = await secondClient.GetRawTextAsync("admin/orders");

        secondResult.IsSuccess.Should().BeTrue();
        secondRecording.RefreshRequests.Should().Be(0);
        secondRecording.ProtectedAuthorizationFingerprints.Should().ContainSingle()
            .Which.Should().Be(persistence.AccessFingerprint);
        secondRecording.ProtectedAuthorizationFingerprints.Should().NotContain(Fingerprint(firstTokens.InitialAccessToken));
        rereadTokens.AccessToken.Should().Be(persistence.AccessToken);
        rereadTokens.RefreshToken.Should().Be(persistence.RefreshToken);
    }

    private ApiClient CreateApiClient(InMemoryTokens tokens, RecordingPersistence persistence, RequestRecording recording,
        SessionTokenRefreshCoordinator? coordinator = null) => new(
            CreateHttpClient(recording), tokens, coordinator ?? CreateCoordinator(recording), persistence,
            new EmptyServiceProvider(), null, NullLogger<ApiClient>.Instance);

    private SessionTokenRefreshCoordinator CreateCoordinator(RequestRecording recording) =>
        new(CreateHttpClient(recording));

    private HttpClient CreateHttpClient(RequestRecording recording) => new(new RecordingHandler(_fixture.Factory.Server.CreateHandler(), recording))
    {
        BaseAddress = new Uri("https://localhost/api/")
    };

    private async Task<(User User, string AccessToken, string RefreshToken)> CreateAdminSessionAsync()
    {
        const string password = "Secure-Test-Password-123!";
        var mobile = $"0935{Random.Shared.Next(1000000, 9999999)}";
        await using (var db = _fixture.CreateDbContext())
        {
            var role = await db.Roles.SingleAsync(x => x.Name == "Admin");
            var user = new User
            {
                Id = Guid.NewGuid(), FullName = "FIX-01B Admin", Mobile = mobile,
                Email = $"fix01b-{Guid.NewGuid():N}@example.test", PasswordHash = PasswordHasher.Hash(password),
                Status = 1, VerificationStatus = 0, IsMobileConfirmed = true, CreatedAt = DateTime.UtcNow
            };
            user.Roles.Add(role);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Mobile = mobile, Password = password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body!.IsSuccess.Should().BeTrue();
        return ((await _fixture.CreateDbContext().Users.SingleAsync(x => x.Mobile == mobile)), body.Data!.AccessToken, body.Data.RefreshToken);
    }

    private async Task<Order> CreatePendingOrderAsync()
    {
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"FIX01B-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
            CurrencyType = (byte)CurrencyType.Toman, CreatedAt = DateTime.UtcNow
        };
        await using var db = _fixture.CreateDbContext();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private async Task RevokeRefreshTokenAsync(string accessToken, string refreshToken)
    {
        using var client = _fixture.CreateClient(accessToken);
        var response = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequestDto { RefreshToken = refreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string ExpiredAccessToken() => "header.eyJleHAiOjF9.signature";
    private static string Fingerprint(string? value) => value is null ? "<none>" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];

    private sealed class RecordingHandler(HttpMessageHandler inner, RequestRecording recording) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var kind = path.EndsWith("/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
                ? "refresh"
                : path.Contains("/admin/orders/", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post
                    ? "mutation" : "protected";
            recording.Events.Enqueue(new RecordedRequest(kind, request.Headers.Authorization is null ? "<none>" : Fingerprint(request.Headers.Authorization.Parameter)));
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class RequestRecording
    {
        public ConcurrentQueue<RecordedRequest> Events { get; } = new();
        public int RefreshRequests => Events.Count(x => x.Kind == "refresh");
        public int MutationRequests => Events.Count(x => x.Kind == "mutation");
        public IEnumerable<string> MutationAuthorizationFingerprints => Events.Where(x => x.Kind == "mutation").Select(x => x.AuthorizationFingerprint);
        public IEnumerable<string> ProtectedAuthorizationFingerprints => Events.Where(x => x.Kind == "protected").Select(x => x.AuthorizationFingerprint);
    }

    private sealed record RecordedRequest(string Kind, string AuthorizationFingerprint);

    private sealed class InMemoryTokens(string scheme, string accessToken, string refreshToken) : IAccessTokenProvider
    {
        public string InitialAccessToken { get; } = accessToken;
        public string? AccessToken { get; private set; } = accessToken;
        public string? RefreshToken { get; private set; } = refreshToken;
        public bool WasCleared { get; private set; }
        public Task<string?> GetAccessTokenAsync() => Task.FromResult(AccessToken);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(RefreshToken);
        public Task<string?> GetSchemeAsync() => Task.FromResult<string?>(scheme);
        public void SetTokens(string _, string access, string refresh) => (AccessToken, RefreshToken) = (access, refresh);
        public void ClearTokens() { WasCleared = true; (AccessToken, RefreshToken) = (null, null); }
    }

    private sealed class RecordingPersistence : ITokenSessionPersistence
    {
        public bool Persisted { get; private set; }
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public string AccessFingerprint => Fingerprint(AccessToken);
        public Task<bool> PersistAsync(string _, string access, string refresh, CancellationToken cancellationToken)
        {
            Persisted = true;
            (AccessToken, RefreshToken) = (access, refresh);
            return Task.FromResult(true);
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
