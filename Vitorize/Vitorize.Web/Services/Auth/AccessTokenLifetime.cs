using System.Text.Json;

namespace Vitorize.Web.Services.Auth;

/// <summary>Reads only the JWT expiry claim to decide when an access token needs rotation.</summary>
public static class AccessTokenLifetime
{
    public static readonly TimeSpan RefreshSafetyWindow = TimeSpan.FromMinutes(2);

    public static bool RequiresRefresh(string? token, DateTimeOffset utcNow, TimeSpan? safetyWindow = null)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var parts = token.Split('.');
        if (parts.Length < 2) return true;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (!json.RootElement.TryGetProperty("exp", out var expiry) || !expiry.TryGetInt64(out var unixSeconds))
                return true;

            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds) <= utcNow.Add(safetyWindow ?? RefreshSafetyWindow);
        }
        catch
        {
            // An unparseable bearer cannot safely be used for a non-idempotent request.
            return true;
        }
    }
}
