using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Registration is gated on a code sent to the customer's mobile.
///
/// The invariant every test here defends: a registration whose code has not been verified must never
/// produce an authenticated session. The account row exists from the first step - that is what makes
/// the mobile unique and keeps the password hashed at rest - but it is created NOT login-eligible, so
/// no sign-in path will accept it until the code is verified.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class RegistrationOtpGateIntegrationTests
{
    private const string Password = "Secure-Test-Password-123!";
    private readonly IntegrationTestFixture _fixture;
    public RegistrationOtpGateIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private static string UnusedMobile() => $"0912{Random.Shared.Next(1000000, 9999999)}";

    // ---------------------------------------------------------------- A: the code is sent

    [Fact]
    public async Task Registering_a_new_mobile_sends_a_verification_code_and_no_tokens()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        var response = await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<RegistrationChallengeDto>>();
        body!.Data!.Outcome.Should().Be(AuthOutcomeCodes.RegistrationOtpSent);
        body.Data.MaskedMobile.Should().NotBeNullOrWhiteSpace();
        body.Data.ExpirySeconds.Should().BeGreaterThan(0);

        // The masked value must not be the whole number.
        body.Data.MaskedMobile.Should().NotBe(mobile);

        Sms().Sent.Should().HaveCount(1, "exactly one code for one registration");
        await using var db = _fixture.CreateDbContext();
        (await db.OtpCodes.CountAsync(x => x.Mobile == mobile &&
            x.Purpose == (byte)OtpPurpose.MobileVerification && x.ConsumedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task The_stored_code_is_hashed_never_kept_in_the_clear()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        await using var db = _fixture.CreateDbContext();
        var otp = await db.OtpCodes.AsNoTracking()
            .Where(x => x.Mobile == mobile && x.Purpose == (byte)OtpPurpose.MobileVerification)
            .OrderByDescending(x => x.CreatedAt).FirstAsync();

        otp.CodeHash.Should().NotBeNullOrWhiteSpace();
        otp.CodeHash.Length.Should().BeGreaterThan(6, "a hash, not the six digits");
        otp.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        otp.MaxAttempt.Should().BeGreaterThan(0, "attempts are capped");
    }

    // ---------------------------------------------------------------- B: no session before verifying

    [Fact]
    public async Task Before_verifying_the_account_cannot_sign_in_by_any_route()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        // Password login: the account exists with the right password but is not login-eligible.
        var password = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Mobile = mobile, Password = Password });
        password.IsSuccessStatusCode.Should().BeFalse("an unverified registration is not a usable account");
        var passwordBody = await password.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        passwordBody!.Data.Should().BeNull();
        passwordBody.ErrorCode.Should().Be(AuthOutcomeCodes.AccountNotEligible);

        // OTP login must not offer itself either.
        var otpLogin = await client.PostAsJsonAsync("/api/auth/login/otp/request", new RequestOtpLoginRequestDto { Mobile = mobile });
        var otpBody = await otpLogin.Content.ReadFromJsonAsync<ApiResult<RequestOtpLoginResponseDto>>();
        otpBody!.Data!.Outcome.Should().Be(AuthOutcomeCodes.AccountNotEligible);

        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Mobile == mobile);
        user.Status.Should().Be((byte)UserStatus.Inactive);
        user.IsMobileConfirmed.Should().BeFalse();
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == user.Id)).Should().Be(0, "no session was issued");
    }

    // ---------------------------------------------------------------- C, D: bad and expired codes

    [Fact]
    public async Task A_wrong_code_does_not_complete_the_registration()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await _fixture.SetPendingRegistrationCodeAsync(mobile, "111111");

        var response = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "999999" });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>())!.Data.Should().BeNull();
        await AssertStillPendingAsync(mobile);
    }

    [Fact]
    public async Task An_expired_code_does_not_complete_the_registration()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await _fixture.SetPendingRegistrationCodeAsync(mobile, "222222");

        await using (var db = _fixture.CreateDbContext())
        {
            var otp = await db.OtpCodes.Where(x => x.Mobile == mobile && x.ConsumedAt == null)
                .OrderByDescending(x => x.CreatedAt).FirstAsync();
            otp.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "222222" });

        response.IsSuccessStatusCode.Should().BeFalse();
        await AssertStillPendingAsync(mobile);
    }

    [Fact]
    public async Task Attempts_are_capped_so_a_code_cannot_be_guessed()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await _fixture.SetPendingRegistrationCodeAsync(mobile, "333333");

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await client.PostAsJsonAsync("/api/auth/register/verify",
                new VerifyRegistrationRequestDto { Mobile = mobile, Code = "000000" });
        }

        // Even the right code is refused once the cap is reached: the code was consumed.
        var response = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "333333" });

        response.IsSuccessStatusCode.Should().BeFalse();
        await AssertStillPendingAsync(mobile);
    }

    // ---------------------------------------------------------------- E: resend

    [Fact]
    public async Task Resending_issues_a_new_code_and_retires_the_previous_one()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await _fixture.SetPendingRegistrationCodeAsync(mobile, "444444");

        var resend = await client.PostAsJsonAsync("/api/auth/register/resend", new ResendRegistrationRequestDto { Mobile = mobile });
        resend.StatusCode.Should().Be(HttpStatusCode.OK);

        // The superseded code must no longer work.
        var stale = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "444444" });
        stale.IsSuccessStatusCode.Should().BeFalse();
        await AssertStillPendingAsync(mobile);

        // The newly issued one does.
        var completed = await _fixture.CompleteRegistrationAsync(client, mobile, "555555");
        completed.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Resending_never_creates_a_second_account()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        await client.PostAsJsonAsync("/api/auth/register/resend", new ResendRegistrationRequestDto { Mobile = mobile });
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile && !x.IsDeleted)).Should().Be(1);
    }

    [Fact]
    public async Task A_completed_account_cannot_ask_for_a_registration_code_again()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await _fixture.RegisterAsync(client, mobile);
        Sms().Clear();

        var resend = await client.PostAsJsonAsync("/api/auth/register/resend", new ResendRegistrationRequestDto { Mobile = mobile });

        resend.IsSuccessStatusCode.Should().BeFalse();
        Sms().Sent.Should().BeEmpty("a real account is not a pending registration");
    }

    // ---------------------------------------------------------------- F, G, H: completion

    [Fact]
    public async Task The_correct_code_confirms_the_mobile_and_authenticates_immediately()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        var session = await _fixture.CompleteRegistrationAsync(client, mobile);

        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.RefreshToken.Should().NotBeNullOrWhiteSpace();
        session.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Usable at once, with no login call in between.
        using var authed = _fixture.CreateClient(session.AccessToken);
        var me = await authed.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<ApiResult<CurrentUserDto>>())!.Data!.Id.Should().Be(session.UserId);
        (await authed.GetAsync("/api/orders")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.Include(x => x.Roles).AsNoTracking().SingleAsync(x => x.Id == session.UserId);
        user.Status.Should().Be((byte)UserStatus.Active);
        user.IsMobileConfirmed.Should().BeTrue();
        user.Roles.Select(x => x.Name).Should().ContainSingle("Customer");
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == user.Id && x.RevokedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task A_consumed_code_cannot_be_used_twice()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await _fixture.CompleteRegistrationAsync(client, mobile, "666666");

        var replay = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "666666" });

        replay.IsSuccessStatusCode.Should().BeFalse();
        await using var db = _fixture.CreateDbContext();
        (await db.UserRefreshTokens.CountAsync(x => x.User.Mobile == mobile && x.RevokedAt == null))
            .Should().Be(1, "the replay issued no second session");
    }

    // ---------------------------------------------------------------- I: duplicate mobile

    [Fact]
    public async Task A_mobile_that_already_has_a_real_account_is_refused_without_sending_a_code()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await _fixture.RegisterAsync(client, mobile);
        Sms().Clear();

        var response = await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        response.IsSuccessStatusCode.Should().BeFalse();
        var body = await response.Content.ReadFromJsonAsync<ApiResult<RegistrationChallengeDto>>();
        body!.ErrorCode.Should().Be(AuthOutcomeCodes.AlreadyRegistered);
        body.Message.Should().Contain("وارد شوید");
        Sms().Sent.Should().BeEmpty("no code may be sent toward an account someone already owns");

        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile && !x.IsDeleted)).Should().Be(1);
    }

    [Fact]
    public async Task An_abandoned_registration_never_blocks_the_real_owner()
    {
        // Somebody starts registering with a number and walks away. The person who actually holds the
        // number must still be able to register, because only they receive the code.
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile, "کاربر رهاشده"));

        var second = await client.PostAsJsonAsync("/api/auth/register", Registration(mobile, "مالک واقعی"));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await _fixture.CompleteRegistrationAsync(client, mobile, "777777");
        session.FullName.Should().Be("مالک واقعی");

        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile && !x.IsDeleted)).Should().Be(1);
    }

    [Fact]
    public async Task A_deactivated_real_account_is_not_treated_as_a_pending_registration()
    {
        // The claimable rule must not become a way to take over a suspended customer's number.
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        var session = await _fixture.RegisterAsync(client, mobile);
        await using (var db = _fixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(x => x.Id == session.UserId);
            user.Status = (byte)UserStatus.Inactive;   // deactivated, but signed in before and confirmed
            await db.SaveChangesAsync();
        }
        Sms().Clear();

        var response = await client.PostAsJsonAsync("/api/auth/register", Registration(mobile, "مهاجم"));

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadFromJsonAsync<ApiResult<RegistrationChallengeDto>>())!
            .ErrorCode.Should().Be(AuthOutcomeCodes.AlreadyRegistered);
        Sms().Sent.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- J, K: purpose isolation

    [Fact]
    public async Task A_login_code_cannot_complete_a_registration()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));

        // Plant a Login-purpose code carrying a known value for the same mobile.
        await using (var db = _fixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(x => x.Mobile == mobile);
            db.OtpCodes.Add(new Vitorize.Domain.Entities.OtpCode
            {
                Id = Guid.NewGuid(), UserId = user.Id, Mobile = mobile,
                Purpose = (byte)OtpPurpose.Login, CodeHash = OtpSecurity.Hash("888888"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5), CreatedAt = DateTime.UtcNow, MaxAttempt = 3
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "888888" });

        response.IsSuccessStatusCode.Should().BeFalse("purpose is part of the lookup");
        await AssertStillPendingAsync(mobile);
    }

    [Fact]
    public async Task A_registration_code_cannot_complete_a_login()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await _fixture.RegisterAsync(client, mobile);

        // A fresh registration-purpose code for an account that is now real.
        await using (var db = _fixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(x => x.Mobile == mobile);
            db.OtpCodes.Add(new Vitorize.Domain.Entities.OtpCode
            {
                Id = Guid.NewGuid(), UserId = user.Id, Mobile = mobile,
                Purpose = (byte)OtpPurpose.MobileVerification, CodeHash = OtpSecurity.Hash("999111"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5), CreatedAt = DateTime.UtcNow, MaxAttempt = 3
            });
            await db.SaveChangesAsync();
        }

        using var scope = _fixture.Factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var act = () => auth.VerifyLoginOtpAsync(new VerifyOtpLoginRequestDto { Mobile = mobile, Code = "999111" });

        await act.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
    }

    // ---------------------------------------------------------------- L: double submit

    [Fact]
    public async Task Submitting_the_registration_form_twice_creates_one_account()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        var first = client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        var second = client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await Task.WhenAll(first, second);

        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile && !x.IsDeleted)).Should().Be(1);
    }

    // ---------------------------------------------------------------- password handling

    [Fact]
    public async Task The_password_is_stored_hashed_at_the_first_step_and_never_returned()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        var response = await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain(Password, "the password must never come back to the caller");
        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Mobile == mobile);
        user.PasswordHash.Should().NotBe(Password);
        BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash).Should().BeTrue("the hash is usable after verification");
    }

    [Fact]
    public async Task The_verification_response_carries_no_code_and_no_password()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/register", Registration(mobile));
        await _fixture.SetPendingRegistrationCodeAsync(mobile, "123456");

        var response = await client.PostAsJsonAsync("/api/auth/register/verify",
            new VerifyRegistrationRequestDto { Mobile = mobile, Code = "123456" });
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain("123456");
        raw.Should().NotContain(Password);
        raw.Should().NotContain("passwordHash");
    }

    // ---------------------------------------------------------------- helpers

    private static RegisterRequestDto Registration(string mobile, string fullName = "کاربر تأیید موبایل") => new()
    {
        FullName = fullName,
        Mobile = mobile,
        Email = $"otp-{Guid.NewGuid():N}@example.test",
        Password = Password
    };

    private async Task AssertStillPendingAsync(string mobile)
    {
        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Mobile == mobile);
        user.Status.Should().Be((byte)UserStatus.Inactive, "registration did not complete");
        user.IsMobileConfirmed.Should().BeFalse();
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == user.Id)).Should().Be(0, "no session was issued");
    }

    private FakeSmsSender Sms() =>
        (FakeSmsSender)_fixture.Factory.Services.GetRequiredService<Vitorize.Application.Interfaces.ISmsSender>();
}
