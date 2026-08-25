using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Administrators can change their own password and, with the right permission, reset someone else's.
///
/// The reset is the sensitive half: it takes over an account without knowing its password, so the
/// tests below prove who *cannot* do it as carefully as who can, and prove that the password never
/// appears in a response or an audit row.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class AdminPasswordManagementIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public AdminPasswordManagementIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private const string OriginalPassword = "Secure-Test-Password-123!";
    private const string ReplacementPassword = "Replacement-Password-456!";

    private async Task<(Guid UserId, string Mobile)> CreateCustomerAsync()
    {
        using var client = _fixture.CreateClient();
        var mobile = $"0912{Random.Shared.Next(1000000, 9999999)}";
        var registered = await _fixture.RegisterAsync(client, mobile);
        return (registered.UserId, mobile);
    }

    private async Task<HttpResponseMessage> LoginAsync(string mobile, string password)
    {
        using var client = _fixture.CreateClient();
        return await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Mobile = mobile, Password = password });
    }

    // ---------------------------------------------------------------- reset another user

    [Fact]
    public async Task An_authorized_admin_replaces_the_password_and_ends_every_session()
    {
        var (userId, mobile) = await CreateCustomerAsync();
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        (await LoginAsync(mobile, OriginalPassword)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the account works before the reset");

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = ReplacementPassword, ConfirmPassword = ReplacementPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(ReplacementPassword, "a password must never be echoed back");

        // Invalid credentials are a deliberate 400: the message must not reveal whether the mobile or
        // the password was the wrong half.
        (await LoginAsync(mobile, OriginalPassword)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the old password must stop working");
        (await LoginAsync(mobile, ReplacementPassword)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the new password must work");

        await using var db = _fixture.CreateDbContext();
        var stillLive = await db.UserRefreshTokens.CountAsync(x =>
            x.UserId == userId && x.RevokedAt == null && x.RevocationReason == null);
        stillLive.Should().Be(1, "only the session created by the verification login above remains");

        var revoked = await db.UserRefreshTokens
            .Where(x => x.UserId == userId && x.RevocationReason == "PasswordResetByAdmin")
            .CountAsync();
        revoked.Should().BeGreaterThan(0, "the sessions held before the reset are ended");
    }

    [Fact]
    public async Task The_reset_is_audited_without_recording_the_password()
    {
        var (userId, _) = await CreateCustomerAsync();
        var (adminUser, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = ReplacementPassword, ConfirmPassword = ReplacementPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        var entry = await db.AuditLogs
            .Where(x => x.ActionType == "UserPasswordReset" && x.EntityId == userId.ToString())
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        entry.Should().NotBeNull("an administrator acting on another account is an audit event");
        entry!.UserId.Should().Be(adminUser.Id, "the trail records who did it");
        entry.EntityName.Should().Be("User");
        (entry.Data ?? string.Empty).Should().NotContain(ReplacementPassword);
    }

    // ---------------------------------------------------------------- authorization, both directions

    [Theory]
    [InlineData("Support")]
    [InlineData("KycViewer")]
    public async Task Roles_without_the_permission_are_refused(string role)
    {
        var (userId, _) = await CreateCustomerAsync();
        var (_, token) = await _fixture.CreateUserAndTokenAsync(role);
        using var client = _fixture.CreateClient(token);

        (await client.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = ReplacementPassword, ConfirmPassword = ReplacementPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        var (userId, _) = await CreateCustomerAsync();
        using var client = _fixture.CreateClient();

        (await client.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = ReplacementPassword, ConfirmPassword = ReplacementPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_admin_role_can_reset_because_it_holds_the_permission()
    {
        // The product requirement: the role that actually administers users must be able to do this.
        AdminPermissions.ForRoles(["Admin"]).Should().Contain(AdminPermissions.UserPasswordReset);
        AdminPermissions.ForRoles(["Admin"]).Should().Contain(AdminPermissions.UserManage);
        AdminPermissions.ForRoles(["Support"]).Should().NotContain(AdminPermissions.UserPasswordReset);
        AdminPermissions.ForRoles(["KycViewer"]).Should().NotContain(AdminPermissions.UserPasswordReset);

        var (userId, mobile) = await CreateCustomerAsync();
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Admin");
        using var admin = _fixture.CreateClient(token);

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = ReplacementPassword, ConfirmPassword = ReplacementPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await LoginAsync(mobile, ReplacementPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- password policy

    [Theory]
    [InlineData("short1!", "Short1!")]          // under eight characters
    [InlineData("", "")]                          // empty
    public async Task A_password_below_policy_is_rejected(string password, string confirm)
    {
        var (userId, mobile) = await CreateCustomerAsync();
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = password, ConfirmPassword = confirm }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await LoginAsync(mobile, OriginalPassword)).StatusCode.Should().Be(HttpStatusCode.OK,
            "a rejected reset must leave the account untouched");
    }

    [Fact]
    public async Task A_mismatched_confirmation_is_rejected()
    {
        var (userId, mobile) = await CreateCustomerAsync();
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password",
            new { NewPassword = ReplacementPassword, ConfirmPassword = "Something-Else-789!" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await LoginAsync(mobile, OriginalPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- own password

    [Fact]
    public async Task An_admin_changes_their_own_password_without_any_special_permission()
    {
        // Support holds none of the user-administration permissions, yet must still be able to change
        // its own password: that endpoint is scoped to the caller, so it needs no grant.
        var mobile = $"0912{Random.Shared.Next(1000000, 9999999)}";
        using (var registrar = _fixture.CreateClient())
        {
            await _fixture.RegisterAsync(registrar, mobile);
        }

        var login = await LoginAsync(mobile, OriginalPassword);
        var session = (await login.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>())!.Data!;
        using var client = _fixture.CreateClient(session.AccessToken);

        (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequestDto
        {
            CurrentPassword = OriginalPassword,
            NewPassword = ReplacementPassword,
            ConfirmNewPassword = ReplacementPassword
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await LoginAsync(mobile, ReplacementPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(mobile, OriginalPassword)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
