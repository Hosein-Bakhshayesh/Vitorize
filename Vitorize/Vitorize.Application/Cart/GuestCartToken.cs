using System.Security.Cryptography;
using System.Text;

namespace Vitorize.Application.Cart;

/// <summary>Opaque 256-bit guest-cart capability utilities. Never log or persist the raw token.</summary>
public static class GuestCartToken
{
    public const int ByteLength = 32;

    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(ByteLength))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool IsWellFormed(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43) return false;
        return token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }

    public static string Hash(string token)
    {
        if (!IsWellFormed(token)) throw new ArgumentException("Invalid guest cart token.", nameof(token));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
