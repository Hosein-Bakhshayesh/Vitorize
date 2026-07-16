using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class AuthenticationExtendedIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public AuthenticationExtendedIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Mobile_OTP_request_verify_and_replay_prevention_work_through_API()
    {
        await _fixture.ConfigureSmsAsync();
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        await using (var db = _fixture.CreateDbContext())
        {
            var stored = await db.Users.SingleAsync(x => x.Id == user.Id);
            stored.IsMobileConfirmed = false;
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        var sent = await client.PostAsJsonAsync("/api/auth/send-otp", new SendOtpRequestDto
        {
            Mobile = user.Mobile, Purpose = (byte)OtpPurpose.MobileVerification
        });
        sent.StatusCode.Should().Be(HttpStatusCode.OK);
        var capture = ((FakeSmsSender)_fixture.Factory.Services.GetRequiredService<ISmsSender>()).Sent.Last();
        capture.TemplateId.Should().Be(1001);
        capture.Parameters.Select(x => x.Name).Should().BeEquivalentTo("CODE", "EXPIRE");
        var code = capture.Parameters.Single(x => x.Name == "CODE").Value;

        var request = new VerifyOtpRequestDto
        {
            Mobile = user.Mobile, Purpose = (byte)OtpPurpose.MobileVerification, Code = code
        };
        (await client.PostAsJsonAsync("/api/auth/verify-otp", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/verify-otp", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var verify = _fixture.CreateDbContext();
        (await verify.Users.SingleAsync(x => x.Id == user.Id)).IsMobileConfirmed.Should().BeTrue();
        (await verify.OtpCodes.SingleAsync(x => x.UserId == user.Id && x.Purpose == (byte)OtpPurpose.MobileVerification))
            .ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Expired_OTP_and_attempt_limit_are_enforced_by_real_persistence()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        await using (var db = _fixture.CreateDbContext())
        {
            db.OtpCodes.Add(new OtpCode
            {
                Id = Guid.NewGuid(), UserId = user.Id, Mobile = user.Mobile,
                CodeHash = OtpSecurity.Hash("123456"), Purpose = (byte)OtpPurpose.Login,
                MaxAttempt = 3, ExpiresAt = DateTime.UtcNow.AddMinutes(-1), CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            Func<Task> act = () => auth.VerifyLoginOtpAsync(new VerifyOtpLoginRequestDto
                { Mobile = user.Mobile, Code = "123456" });
            await act.Should().ThrowAsync<Exception>();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            db.OtpCodes.Add(new OtpCode
            {
                Id = Guid.NewGuid(), UserId = user.Id, Mobile = user.Mobile,
                CodeHash = OtpSecurity.Hash("654321"), Purpose = (byte)OtpPurpose.ForgotPassword,
                MaxAttempt = 3, ExpiresAt = DateTime.UtcNow.AddMinutes(3), CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            Func<Task> act = () => auth.ResetPasswordAsync(new ResetPasswordRequestDto
            {
                Mobile = user.Mobile, Code = "000000",
                NewPassword = "Another-Secure-Password-123!",
                ConfirmNewPassword = "Another-Secure-Password-123!"
            });
            await act.Should().ThrowAsync<Exception>();
        }

        await using var verify = _fixture.CreateDbContext();
        var exhausted = await verify.OtpCodes.SingleAsync(x => x.UserId == user.Id && x.Purpose == (byte)OtpPurpose.ForgotPassword);
        exhausted.AttemptCount.Should().Be(3);
        exhausted.ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Forgot_and_reset_password_rotate_credentials_and_revoke_refresh_tokens()
    {
        await _fixture.ConfigureSmsAsync();
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        await using (var db = _fixture.CreateDbContext())
        {
            db.UserRefreshTokens.Add(new UserRefreshToken
            {
                Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                ExpiresAt = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequestDto { Mobile = user.Mobile })).StatusCode.Should().Be(HttpStatusCode.OK);
        var capture = ((FakeSmsSender)_fixture.Factory.Services.GetRequiredService<ISmsSender>()).Sent.Last();
        var code = capture.Parameters.Single(x => x.Name == "CODE").Value;
        const string newPassword = "Reset-Secure-Password-123!";
        (await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto
        {
            Mobile = user.Mobile, Code = code, NewPassword = newPassword, ConfirmNewPassword = newPassword
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verify = _fixture.CreateDbContext();
        var stored = await verify.Users.SingleAsync(x => x.Id == user.Id);
        BCrypt.Net.BCrypt.Verify(newPassword, stored.PasswordHash).Should().BeTrue();
        (await verify.UserRefreshTokens.Where(x => x.UserId == user.Id).ToListAsync())
            .Should().OnlyContain(x => x.RevokedAt != null && x.RevocationReason == "PasswordReset");
    }

    [Fact]
    public async Task Profile_update_and_password_change_persist_and_revoke_sessions()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await using (var db = _fixture.CreateDbContext())
        {
            db.UserRefreshTokens.Add(new UserRefreshToken
            {
                Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                ExpiresAt = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var client = _fixture.CreateClient(token);
        var email = $"updated-{Guid.NewGuid():N}@example.test";
        (await client.PutAsJsonAsync("/api/auth/profile",
            new UpdateProfileRequestDto { FullName = "Updated Integration User", Email = email })).StatusCode
            .Should().Be(HttpStatusCode.OK);

        const string current = "Secure-Test-Password-123!";
        const string changed = "Changed-Secure-Password-123!";
        (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequestDto
        {
            CurrentPassword = current, NewPassword = changed, ConfirmNewPassword = changed
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verify = _fixture.CreateDbContext();
        var stored = await verify.Users.SingleAsync(x => x.Id == user.Id);
        stored.FullName.Should().Be("Updated Integration User");
        stored.Email.Should().Be(email);
        stored.IsEmailConfirmed.Should().BeFalse();
        BCrypt.Net.BCrypt.Verify(changed, stored.PasswordHash).Should().BeTrue();
        (await verify.UserRefreshTokens.SingleAsync(x => x.UserId == user.Id)).RevocationReason
            .Should().Be("PasswordChanged");
    }
}
