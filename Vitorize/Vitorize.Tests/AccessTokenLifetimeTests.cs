using System.Text;
using System.Text.Json;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

public sealed class AccessTokenLifetimeTests
{
    [Fact]
    public void Token_inside_safety_window_requires_refresh()
    {
        Assert.True(AccessTokenLifetime.RequiresRefresh(Token(DateTimeOffset.UtcNow.AddMinutes(1)), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Token_outside_safety_window_does_not_require_refresh()
    {
        Assert.False(AccessTokenLifetime.RequiresRefresh(Token(DateTimeOffset.UtcNow.AddMinutes(5)), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Missing_or_malformed_expiry_is_not_used_for_a_mutation()
    {
        Assert.True(AccessTokenLifetime.RequiresRefresh("not-a-jwt", DateTimeOffset.UtcNow));
        Assert.True(AccessTokenLifetime.RequiresRefresh("header.e30.signature", DateTimeOffset.UtcNow));
    }

    private static string Token(DateTimeOffset expiry)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { exp = expiry.ToUnixTimeSeconds() })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"header.{payload}.signature";
    }
}
