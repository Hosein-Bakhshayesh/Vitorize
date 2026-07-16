using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vitorize.Application.Common;
using Vitorize.Infrastructure.Services;
using Xunit;

namespace Vitorize.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class AuthenticationUnitTests
{
    [Fact]
    public void Password_hash_is_salted_and_verifiable()
    {
        const string password = "A-Strong-Password!42";

        var first = PasswordHasher.Hash(password);
        var second = PasswordHasher.Hash(password);

        first.Should().NotBe(password).And.NotBe(second);
        PasswordHasher.Verify(password, first).Should().BeTrue();
        PasswordHasher.Verify("wrong-password", first).Should().BeFalse();
    }

    [Fact]
    public void Access_token_contains_identity_role_and_derived_permissions()
    {
        var user = new UserBuilder().WithRole("Admin").Build();
        var sut = new JwtTokenService(Options.Create(UnitFixtures.JwtSettings));

        var encoded = sut.GenerateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(encoded.Split('.')[1]));
        var root = payload.RootElement;

        token.Issuer.Should().Be(UnitFixtures.JwtSettings.Issuer);
        root.GetProperty("aud").GetString().Should().Be(UnitFixtures.JwtSettings.Audience);
        token.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Sub && x.Value == user.Id.ToString());
        root.GetProperty("mobile").GetString().Should().Be(user.Mobile);
        token.Claims.Should().Contain(x => x.Type == "fullname" && x.Value == user.FullName);
        root.EnumerateObject().Should().Contain(x =>
            x.Name.EndsWith("/role", StringComparison.OrdinalIgnoreCase) && x.Value.GetString() == "Admin");
        token.Claims.Where(x => x.Type == AdminPermissions.ClaimType).Select(x => x.Value)
            .Should().BeEquivalentTo(AdminPermissions.OrderFulfillment, AdminPermissions.KycReview, AdminPermissions.SettingsManage);
        var expires = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("exp").GetInt64()).UtcDateTime;
        expires.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
        expires.Should().BeBefore(DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void SuperAdmin_token_receives_every_permission_without_duplicates()
    {
        var user = new UserBuilder().WithRole("SuperAdmin").WithRole("Admin").Build();
        var sut = new JwtTokenService(Options.Create(UnitFixtures.JwtSettings));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerateAccessToken(user));
        var permissions = token.Claims.Where(x => x.Type == AdminPermissions.ClaimType).Select(x => x.Value).ToArray();

        permissions.Should().BeEquivalentTo(AdminPermissions.All);
        permissions.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Refresh_tokens_are_random_512_bit_values()
    {
        var sut = new JwtTokenService(Options.Create(UnitFixtures.JwtSettings));

        var first = sut.GenerateRefreshToken();
        var second = sut.GenerateRefreshToken();

        Convert.FromBase64String(first).Should().HaveCount(64);
        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData("Admin", AdminPermissions.OrderFulfillment, true)]
    [InlineData("Admin", AdminPermissions.FinanceManage, false)]
    [InlineData("Support", AdminPermissions.OrderFulfillment, true)]
    [InlineData("Support", AdminPermissions.SettingsManage, false)]
    [InlineData("Customer", AdminPermissions.OrderFulfillment, false)]
    public void Role_permission_mapping_obeys_least_privilege(string role, string permission, bool expected)
    {
        AdminPermissions.ForRoles([role]).Contains(permission).Should().Be(expected);
    }

    [Fact]
    public void Otp_hash_handles_null_consistently_and_rejects_invalid_stored_hash()
    {
        OtpSecurity.Hash(null!).Should().Be(OtpSecurity.Hash(string.Empty));
        OtpSecurity.Verify("123456", null!).Should().BeFalse();
    }
}
