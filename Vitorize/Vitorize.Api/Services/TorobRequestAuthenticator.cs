using System.Globalization;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Vitorize.Api.Services;

/// <summary>
/// What the request carried in <c>X-Torob-Token</c>, decoded for logging and (only when configured) enforced.
/// The token text itself is never stored; only its length and claims.
/// </summary>
public sealed record TorobTokenInfo(
    bool Present,
    int Length,
    bool LooksLikeJwt,
    string? Algorithm,
    string? HeaderVersion,
    IReadOnlyList<string> Audiences,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? NotBefore,
    bool AudienceMatches,
    bool TimeValid,
    bool? SignatureValid,
    string? VersionHeader,
    string? ParseError)
{
    public static readonly TorobTokenInfo Missing = new(false, 0, false, null, null, [], null, null, false, false, null, null, null);
}

public interface ITorobRequestAuthenticator
{
    /// <summary>Never throws and never rejects on its own; describes the token so the controller can log and decide.</summary>
    TorobTokenInfo Inspect(HttpRequest request);

    /// <summary>True only when <c>Torob:EnforceToken</c> is enabled and the token is absent or invalid.</summary>
    bool ShouldReject(TorobTokenInfo token, out string error);
}

/// <summary>
/// Torob signs every request with an Ed25519 (EdDSA) JWT whose <c>aud</c> is the Host the crawler called
/// (https://panel.torob.com/s/torob_api_token_guide). The signature, expiry window and audience are checked
/// against Torob's published public key and the result is logged. Requests are rejected only once
/// <c>Torob:EnforceToken</c> is switched on, after real crawler tokens have been seen to verify in the log.
/// </summary>
public sealed class TorobRequestAuthenticator : ITorobRequestAuthenticator
{
    public const string TokenHeader = "X-Torob-Token";

    /// <summary>The key published under "کلید عمومی ترب" (SubjectPublicKeyInfo, base64). Overridable via Torob:PublicKey.</summary>
    public const string OfficialPublicKey = "MCowBQYDK2VwAyEAt6Mu4T0pBORY11W+QeM35UsmLO3vsf+6yKpFDEImFk0=";

    private static readonly string[] VersionHeaders = ["C-Torob-Token-Version", "X-Torob-Token-Version"];

    private readonly string? _expectedAudience;
    private readonly bool _enforce;
    private readonly TimeSpan _clockSkew;
    private readonly TimeProvider _clock;
    private readonly Ed25519PublicKeyParameters? _publicKey;
    private readonly string? _publicKeyError;

    public TorobRequestAuthenticator(IConfiguration configuration, TimeProvider? clock = null)
    {
        var audience = configuration["Torob:ExpectedAudience"];
        _expectedAudience = string.IsNullOrWhiteSpace(audience) ? null : audience.Trim();
        _enforce = bool.TryParse(configuration["Torob:EnforceToken"], out var enforce) && enforce;
        _clockSkew = TimeSpan.FromSeconds(
            int.TryParse(configuration["Torob:ClockSkewSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var skew) && skew >= 0 ? skew : 300);
        _clock = clock ?? TimeProvider.System;
        (_publicKey, _publicKeyError) = LoadPublicKey(configuration["Torob:PublicKey"]);
    }

    public TorobTokenInfo Inspect(HttpRequest request)
    {
        string? version = null;
        foreach (var name in VersionHeaders)
        {
            if (request.Headers.TryGetValue(name, out var values) && !string.IsNullOrWhiteSpace(values.ToString()))
            {
                version = values.ToString().Trim();
                break;
            }
        }

        if (!request.Headers.TryGetValue(TokenHeader, out var header) || string.IsNullOrWhiteSpace(header.ToString()))
            return TorobTokenInfo.Missing with { VersionHeader = version };

        return Decode(header.ToString().Trim()) with { VersionHeader = version };
    }

    public bool ShouldReject(TorobTokenInfo token, out string error)
    {
        error = string.Empty;
        if (!_enforce) return false;
        if (!token.Present) error = "توکن ترب ارسال نشده است.";
        else if (!token.LooksLikeJwt) error = "توکن ترب معتبر نیست.";
        else if (token.SignatureValid != true) error = "امضای توکن ترب معتبر نیست.";
        else if (!token.TimeValid) error = "توکن ترب منقضی شده یا هنوز معتبر نیست.";
        else if (!token.AudienceMatches) error = "مخاطب (aud) توکن ترب با این دامنه مطابقت ندارد.";
        return error.Length > 0;
    }

    /// <summary>Decodes header and payload without trusting them, then verifies the EdDSA signature.</summary>
    public TorobTokenInfo Decode(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return TorobTokenInfo.Missing with { Present = true, Length = token.Length, ParseError = "not-a-jwt" };

        try
        {
            using var header = JsonDocument.Parse(Base64Url(parts[0]));
            using var payload = JsonDocument.Parse(Base64Url(parts[1]));
            if (header.RootElement.ValueKind != JsonValueKind.Object || payload.RootElement.ValueKind != JsonValueKind.Object)
                return TorobTokenInfo.Missing with { Present = true, Length = token.Length, ParseError = "not-a-jwt" };

            var algorithm = GetText(header.RootElement, "alg");
            var headerVersion = GetText(header.RootElement, "v");
            var audiences = GetAudiences(payload.RootElement);
            var expiresAt = GetUnixTime(payload.RootElement, "exp");
            var notBefore = GetUnixTime(payload.RootElement, "nbf");
            var now = _clock.GetUtcNow();
            var timeValid = expiresAt is not null &&
                            now <= expiresAt.Value + _clockSkew &&
                            (notBefore is null || now >= notBefore.Value - _clockSkew);
            var audienceMatches = _expectedAudience is not null &&
                                  audiences.Contains(_expectedAudience, StringComparer.OrdinalIgnoreCase);

            return new TorobTokenInfo(
                Present: true,
                Length: token.Length,
                LooksLikeJwt: true,
                Algorithm: algorithm,
                HeaderVersion: headerVersion,
                Audiences: audiences,
                ExpiresAt: expiresAt,
                NotBefore: notBefore,
                AudienceMatches: audienceMatches,
                TimeValid: timeValid,
                SignatureValid: VerifySignature(parts, algorithm),
                VersionHeader: null,
                ParseError: _publicKeyError);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return TorobTokenInfo.Missing with { Present = true, Length = token.Length, ParseError = exception.GetType().Name };
        }
    }

    private bool? VerifySignature(string[] parts, string? algorithm)
    {
        if (_publicKey is null) return null;
        if (!string.Equals(algorithm, "EdDSA", StringComparison.OrdinalIgnoreCase)) return false;

        byte[] signature;
        try
        {
            signature = Base64Url(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }
        if (signature.Length != Ed25519PrivateKeyParameters.SignatureSize) return false;

        var data = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        var signer = new Ed25519Signer();
        signer.Init(false, _publicKey);
        signer.BlockUpdate(data, 0, data.Length);
        return signer.VerifySignature(signature);
    }

    private static (Ed25519PublicKeyParameters? Key, string? Error) LoadPublicKey(string? configured)
    {
        var text = string.IsNullOrWhiteSpace(configured) ? OfficialPublicKey : configured;
        var base64 = string.Concat(text
            .Split('\n', '\r')
            .Where(line => !line.Contains("PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim()));
        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length == Ed25519PublicKeyParameters.KeySize) return (new Ed25519PublicKeyParameters(bytes, 0), null);
            return PublicKeyFactory.CreateKey(bytes) is Ed25519PublicKeyParameters key
                ? (key, null)
                : (null, "public-key-not-ed25519");
        }
        catch (Exception exception)
        {
            // A misconfigured key must not take the endpoint down; verification simply reports "unknown".
            return (null, $"public-key-unreadable:{exception.GetType().Name}");
        }
    }

    private static string? GetText(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null
        };
    }

    private static IReadOnlyList<string> GetAudiences(JsonElement payload)
    {
        if (!payload.TryGetProperty("aud", out var aud)) return [];
        return aud.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(aud.GetString()) ? [] : [aud.GetString()!.Trim()],
            JsonValueKind.Array => aud.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                .Select(item => item.GetString()!.Trim())
                .ToArray(),
            _ => []
        };
    }

    private static DateTimeOffset? GetUnixTime(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value)) return null;
        long seconds;
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetInt64(out var integer):
                seconds = integer;
                break;
            case JsonValueKind.Number when value.TryGetDouble(out var real):
                seconds = (long)real;
                break;
            case JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                seconds = parsed;
                break;
            default:
                return null;
        }
        // DateTimeOffset.FromUnixTimeSeconds throws outside year 0001..9999; treat such claims as absent.
        return seconds is < -62_135_596_800 or > 253_402_300_799 ? null : DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    private static byte[] Base64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}
