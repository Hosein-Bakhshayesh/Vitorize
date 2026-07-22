using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The admin cookie is built from the access token by <see cref="JwtHelper"/>. These tests pin the
/// full API-token to Web-extraction pipeline: an Admin/SuperAdmin token must be recognised as admin
/// (with the expected permissions) and a customer token must not - guarding against a "missing role
/// claim / permission claim not loaded" regression that would silently deny the admin panel.
/// </summary>
public sealed class JwtTokenServiceClaimsTests
{
    private static JwtTokenService Service() => new(Options.Create(new JwtSettings
    {
        SecretKey = "unit-tests-jwt-secret-key-0000000000000000",
        Issuer = "Vitorize.Api.UnitTests",
        Audience = "Vitorize.UnitTests",
        AccessTokenExpirationMinutes = 5,
        RefreshTokenExpirationDays = 1
    }));

    private static User UserWithRoles(params string[] roleNames)
    {
        var user = new User { Id = Guid.NewGuid(), FullName = "Claims principal", Mobile = "09120000000" };
        foreach (var name in roleNames)
            user.Roles.Add(new Role { Id = Guid.NewGuid(), Name = name });
        return user;
    }

    [Fact]
    public void Admin_token_is_recognized_as_admin_with_permissions_by_the_web_extractor()
    {
        var token = Service().GenerateAccessToken(UserWithRoles("Admin"));

        var roles = JwtHelper.ExtractRoles(token);
        Assert.Contains("Admin", roles);
        Assert.True(JwtHelper.IsAdmin(roles));

        var permissions = JwtHelper.ExtractPermissions(token);
        Assert.Contains(AdminPermissions.OrderFulfillment, permissions);
        Assert.Contains(AdminPermissions.KycReview, permissions);
        Assert.Contains(AdminPermissions.SettingsManage, permissions);
    }

    [Fact]
    public void SuperAdmin_token_is_admin_with_all_permissions()
    {
        var token = Service().GenerateAccessToken(UserWithRoles("SuperAdmin"));

        Assert.True(JwtHelper.IsAdmin(JwtHelper.ExtractRoles(token)));
        var permissions = JwtHelper.ExtractPermissions(token).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in AdminPermissions.All)
            Assert.Contains(permission, permissions);
    }

    [Fact]
    public void Customer_token_is_not_recognized_as_admin_and_has_no_permissions()
    {
        var token = Service().GenerateAccessToken(UserWithRoles("Customer"));

        var roles = JwtHelper.ExtractRoles(token);
        Assert.Contains("Customer", roles);
        Assert.False(JwtHelper.IsAdmin(roles));
        Assert.Empty(JwtHelper.ExtractPermissions(token));
    }

    [Fact]
    public void Role_matching_is_case_insensitive()
    {
        // Guards against a role-name casing regression breaking admin recognition.
        Assert.True(JwtHelper.IsAdmin(new[] { "admin" }));
        Assert.True(JwtHelper.IsAdmin(new[] { "SUPERADMIN" }));
        Assert.False(JwtHelper.IsAdmin(new[] { "customer" }));
    }
}
