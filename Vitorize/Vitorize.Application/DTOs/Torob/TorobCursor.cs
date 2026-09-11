using System.Text.Json;

namespace Vitorize.Application.DTOs.Torob;

/// <summary>Opaque continuation token retaining the last offer ID and the next page number.</summary>
public sealed record TorobCursor(string LastId, int Page)
{
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string value, out TorobCursor? cursor)
    {
        cursor = null;
        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            cursor = JsonSerializer.Deserialize<TorobCursor>(Convert.FromBase64String(base64));
            return cursor is { Page: > 1 and < int.MaxValue } && !string.IsNullOrWhiteSpace(cursor.LastId);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }
}
