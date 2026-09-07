using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Vitorize.Api.Services;

/// <summary>Validates the Ed25519 JWT that Torob sends with every API v3 request.</summary>
public interface ITorobRequestAuthenticator
{
    bool TryValidate(HttpRequest request, out string error);
}

public sealed class TorobRequestAuthenticator : ITorobRequestAuthenticator
{
    // The raw 32-byte Ed25519 key is the subjectPublicKey portion of Torob's published PEM.
    private static readonly byte[] PublicKey = Convert.FromBase64String("t6Mu4T0pBORY11W+QeM35UsmLO3vsf+6yKpFDEImFk0=");
    private readonly string _expectedAudience;

    public TorobRequestAuthenticator(IConfiguration configuration)
    {
        _expectedAudience = (configuration["Torob:ExpectedAudience"] ?? "vitorize.com").Trim();
    }

    public bool TryValidate(HttpRequest request, out string error)
    {
        error = "";
        if (!request.Headers.TryGetValue("X-Torob-Token-Version", out var version) ||
            !string.Equals(version.ToString(), "1", StringComparison.Ordinal))
        {
            error = "نسخه توکن ترب معتبر نیست.";
            return false;
        }

        if (!request.Headers.TryGetValue("X-Torob-Token", out var tokenHeader))
        {
            error = "توکن ترب ارسال نشده است.";
            return false;
        }

        var token = tokenHeader.ToString();
        if (token.Length is 0 or > 8_192)
        {
            error = "توکن ترب معتبر نیست.";
            return false;
        }

        var segments = token.Split('.');
        if (segments.Length != 3 ||
            !TryBase64UrlDecode(segments[0], out var headerBytes) ||
            !TryBase64UrlDecode(segments[1], out var payloadBytes) ||
            !TryBase64UrlDecode(segments[2], out var signature))
        {
            error = "ساختار توکن ترب معتبر نیست.";
            return false;
        }

        if (signature.Length != 64 || !HasExpectedAlgorithm(headerBytes))
        {
            error = "امضای توکن ترب معتبر نیست.";
            return false;
        }

        var signingInput = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(PublicKey, 0));
        verifier.BlockUpdate(signingInput, 0, signingInput.Length);
        if (!verifier.VerifySignature(signature))
        {
            error = "امضای توکن ترب معتبر نیست.";
            return false;
        }

        return HasValidClaims(payloadBytes, out error);
    }

    private bool HasValidClaims(byte[] payloadBytes, out string error)
    {
        error = "";
        try
        {
            using var document = JsonDocument.Parse(payloadBytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("aud", out var audience) ||
                audience.ValueKind != JsonValueKind.String ||
                !string.Equals(audience.GetString(), _expectedAudience, StringComparison.OrdinalIgnoreCase))
            {
                error = "مخاطب توکن ترب معتبر نیست.";
                return false;
            }

            if (!TryUnixTimestamp(root, "exp", out var expiresAt) ||
                !TryUnixTimestamp(root, "nbf", out var notBefore))
            {
                error = "زمان اعتبار توکن ترب معتبر نیست.";
                return false;
            }

            // Small tolerance covers harmless clock skew, while still enforcing both JWT bounds.
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (expiresAt < now - 60 || notBefore > now + 60)
            {
                error = "توکن ترب منقضی شده یا هنوز معتبر نیست.";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            error = "داده‌های توکن ترب معتبر نیست.";
            return false;
        }
    }

    private static bool HasExpectedAlgorithm(byte[] headerBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(headerBytes);
            return document.RootElement.TryGetProperty("alg", out var algorithm) &&
                   algorithm.ValueKind == JsonValueKind.String &&
                   string.Equals(algorithm.GetString(), "EdDSA", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryUnixTimestamp(JsonElement root, string name, out long value)
    {
        value = 0;
        return root.TryGetProperty(name, out var property) && property.TryGetInt64(out value);
    }

    private static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            return false;
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
