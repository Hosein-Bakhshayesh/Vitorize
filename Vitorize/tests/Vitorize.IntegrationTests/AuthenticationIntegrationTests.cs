using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BCrypt.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Auth;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class AuthenticationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public AuthenticationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Registration_login_refresh_rotation_logout_and_duplicate_protection_work_end_to_end()
    {
        using var client = _fixture.CreateClient();
        var mobile = $"0912{Random.Shared.Next(1000000, 9999999)}";
        const string password = "Secure-Test-Password-123!";
        var registered = await _fixture.RegisterAsync(client, mobile);

        await using (var db = _fixture.CreateDbContext())
        {
            var stored = await db.Users.Include(x => x.Roles).SingleAsync(x => x.Id == registered.UserId);
            stored.PasswordHash.Should().NotBe(password);
            BCrypt.Net.BCrypt.Verify(password, stored.PasswordHash).Should().BeTrue();
            stored.Roles.Select(x => x.Name).Should().ContainSingle("Customer");
            (await db.UserRefreshTokens.CountAsync(x => x.UserId == registered.UserId && x.RevokedAt == null))
                .Should().Be(1);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Mobile = mobile,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "Duplicate", Mobile = mobile, Password = password
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = registered.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await refreshResponse.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>())!.Data!;
        rotated.RefreshToken.Should().NotBe(registered.RefreshToken);

        // Replaying the spent token immediately is a race, not an attack: two tabs, or a reload whose
        // rotated cookie never landed. Inside the grace window it therefore returns the SAME pair the
        // rotation produced rather than 401, so a live session is not destroyed by timing. It must not
        // start a second token chain - that is what the equality check below proves.
        var replay = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = registered.RefreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayed = (await replay.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>())!.Data!;
        replayed.RefreshToken.Should().Be(rotated.RefreshToken);
        replayed.AccessToken.Should().Be(rotated.AccessToken);

        await using (var db = _fixture.CreateDbContext())
        {
            // The replay reused the recorded result instead of rotating again, so it created no extra
            // token chain. Two remain live because this test also signed in separately above; what
            // matters is that replaying did not add a third.
            (await db.UserRefreshTokens.CountAsync(x => x.UserId == registered.UserId && x.RevokedAt == null))
                .Should().Be(2);
        }

        // A token that is neither active nor inside the window is a genuine replay. It is refused, and
        // because a replayed token means the family can no longer be trusted, every session ends.
        var stale = await _fixture.ForgetRotationGraceAsync(registered.RefreshToken);
        stale.Should().BeTrue();
        var refused = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = registered.RefreshToken });
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using (var db = _fixture.CreateDbContext())
        {
            (await db.UserRefreshTokens.CountAsync(x => x.UserId == registered.UserId && x.RevokedAt == null))
                .Should().Be(0);
        }

        // Logout no longer needs a live access token. Requiring one meant that signing out after the
        // access token had expired silently skipped revocation and left the refresh token usable for
        // the rest of its life, because the Web client never retries a POST after a 401. The refresh
        // token in the body is the proof of possession, and the operation only ever revokes.
        var logout = await client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequestDto { RefreshToken = rotated.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.OK,
            "the refresh token is itself the proof, so an expired access token must not block revocation");

        // Idempotent: repeating it with a live access token is still fine.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        (await client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequestDto { RefreshToken = rotated.RefreshToken })).StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = null;
        (await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = rotated.RefreshToken })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_credentials_return_safe_error_without_password_or_hash()
    {
        using var client = _fixture.CreateClient();
        const string secret = "this-must-never-be-reflected";
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Mobile = "09120000000",
            Password = secret
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(secret).And.NotContain("PasswordHash");
    }
}
