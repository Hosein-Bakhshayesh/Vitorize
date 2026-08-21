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
/// Signing in with a mobile that has no account.
///
/// The reported defect was that requesting a login code for an unregistered number returned an
/// ordinary success, so the UI advanced to the code-entry step even though no message had been sent.
/// The response now names the outcome, and these tests hold the two halves of that together: the
/// outcome is distinguishable, and still nothing is sent and no code is created.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class UnknownUserLoginIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public UnknownUserLoginIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private static string UnusedMobile() => $"0912{Random.Shared.Next(1000000, 9999999)}";

    // ---------------------------------------------------------------- OTP login

    [Fact]
    public async Task Requesting_a_login_code_for_an_unregistered_mobile_reports_that_registration_is_needed()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        var response = await client.PostAsJsonAsync("/api/auth/login/otp/request",
            new RequestOtpLoginRequestDto { Mobile = mobile });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<RequestOtpLoginResponseDto>>();
        body!.Data!.Outcome.Should().Be(AuthOutcomeCodes.RequiresRegistration);
    }

    [Fact]
    public async Task No_message_is_sent_and_no_code_is_created_for_an_unregistered_mobile()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        await client.PostAsJsonAsync("/api/auth/login/otp/request",
            new RequestOtpLoginRequestDto { Mobile = mobile });

        Sms().Sent.Should().BeEmpty("nothing may be sent to a number with no account");
        await using var db = _fixture.CreateDbContext();
        (await db.OtpCodes.CountAsync(x => x.Mobile == mobile)).Should().Be(0);
    }

    [Fact]
    public async Task A_registered_mobile_still_receives_its_login_code()
    {
        // The other half of the contract: making the unknown case distinguishable must not change
        // the working path.
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await _fixture.RegisterAsync(client, mobile);
        Sms().Clear();

        var response = await client.PostAsJsonAsync("/api/auth/login/otp/request",
            new RequestOtpLoginRequestDto { Mobile = mobile });

        var body = await response.Content.ReadFromJsonAsync<ApiResult<RequestOtpLoginResponseDto>>();
        body!.Data!.Outcome.Should().Be(AuthOutcomeCodes.OtpSent);
        body.Data.MaskedMobile.Should().NotBeNullOrWhiteSpace();
        Sms().Sent.Should().HaveCount(1);
        await using var db = _fixture.CreateDbContext();
        (await db.OtpCodes.CountAsync(x => x.Mobile == mobile && x.Purpose == (byte)OtpPurpose.Login))
            .Should().Be(1);
    }

    [Fact]
    public async Task An_existing_but_inactive_account_is_never_told_to_register_again()
    {
        // Telling a suspended customer to register would be wrong, and would invite a duplicate
        // account for a mobile the shop already knows.
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        var registered = await _fixture.RegisterAsync(client, mobile);
        await using (var db = _fixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(x => x.Id == registered.UserId);
            user.Status = (byte)UserStatus.Suspended;
            await db.SaveChangesAsync();
        }
        Sms().Clear();

        var response = await client.PostAsJsonAsync("/api/auth/login/otp/request",
            new RequestOtpLoginRequestDto { Mobile = mobile });

        var body = await response.Content.ReadFromJsonAsync<ApiResult<RequestOtpLoginResponseDto>>();
        body!.Data!.Outcome.Should().Be(AuthOutcomeCodes.AccountNotEligible);
        Sms().Sent.Should().BeEmpty("an inactive account may not be signed in either");
    }

    [Fact]
    public async Task An_unregistered_mobile_cannot_verify_a_code_it_never_received()
    {
        await _fixture.ConfigureSmsAsync();
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await client.PostAsJsonAsync("/api/auth/login/otp/request", new RequestOtpLoginRequestDto { Mobile = mobile });

        var verify = await client.PostAsJsonAsync("/api/auth/login/otp/verify",
            new VerifyOtpLoginRequestDto { Mobile = mobile, Code = "123456" });

        verify.IsSuccessStatusCode.Should().BeFalse("no session may be established without a real code");
        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile)).Should().Be(0, "no account is created by trying");
    }

    // ---------------------------------------------------------------- password login

    [Fact]
    public async Task Password_login_with_an_unregistered_mobile_reports_that_registration_is_needed()
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Mobile = UnusedMobile(),
            Password = "Whatever-Password-123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body!.ErrorCode.Should().Be(AuthOutcomeCodes.RequiresRegistration);
        body.Data.Should().BeNull("no session is established");
    }

    [Fact]
    public async Task A_wrong_password_on_an_existing_account_stays_a_credential_error()
    {
        // These three cases must not collapse into one another: this one keeps its own outcome and
        // its own message, which does not reveal which half was wrong.
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        await _fixture.RegisterAsync(client, mobile);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Mobile = mobile,
            Password = "Definitely-Not-The-Password-9!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body!.ErrorCode.Should().Be(AuthOutcomeCodes.InvalidCredentials);
        body.ErrorCode.Should().NotBe(AuthOutcomeCodes.RequiresRegistration);
        body.Message.Should().NotContain("ثبت‌نام");
    }

    [Fact]
    public async Task A_correct_password_on_an_existing_account_still_signs_in()
    {
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        var registered = await _fixture.RegisterAsync(client, mobile);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Mobile = mobile,
            Password = "Secure-Test-Password-123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body!.Data!.UserId.Should().Be(registered.UserId);
        body.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task An_inactive_account_with_the_right_password_is_refused_without_being_sent_to_register()
    {
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        var registered = await _fixture.RegisterAsync(client, mobile);
        await using (var db = _fixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(x => x.Id == registered.UserId);
            user.Status = (byte)UserStatus.Blocked;
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Mobile = mobile,
            Password = "Secure-Test-Password-123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body!.ErrorCode.Should().Be(AuthOutcomeCodes.AccountNotEligible);
    }

    // ---------------------------------------------------------------- disclosure limits

    [Fact]
    public async Task The_unknown_mobile_response_discloses_nothing_beyond_the_fact_itself()
    {
        // The product requires saying "not registered". It must not become a channel for anything
        // else: no identifier, no email, no verification state, no other identity.
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        var otp = await client.PostAsJsonAsync("/api/auth/login/otp/request",
            new RequestOtpLoginRequestDto { Mobile = mobile });
        var otpJson = await otp.Content.ReadAsStringAsync();
        var password = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Mobile = mobile, Password = "x" });
        var passwordJson = await password.Content.ReadAsStringAsync();

        foreach (var json in new[] { otpJson, passwordJson })
        {
            json.Should().NotContain("userId");
            json.Should().NotContain("email");
            json.Should().NotContain("verificationStatus");
            json.Should().NotContain("passwordHash");
        }
    }

    private FakeSmsSender Sms() =>
        (FakeSmsSender)_fixture.Factory.Services.GetRequiredService<ISmsSender>();
}
