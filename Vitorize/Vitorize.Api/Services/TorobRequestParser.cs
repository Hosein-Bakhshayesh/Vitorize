using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Shared.Logging;

namespace Vitorize.Api.Services;

public enum TorobRequestMode
{
    Invalid,
    PageUrls,
    PageUniques,
    Cursor,
    Page
}

/// <summary>Outcome of reading a Torob request body: a normalised request, or the documented 400 error text.</summary>
public sealed class TorobRequestParseResult
{
    public static readonly TorobRequestParseResult None = new() { Error = TorobRequestParser.EmptyBodyError };

    public TorobProductsRequest? Request { get; init; }
    public TorobRequestMode Mode { get; init; }
    public string? Error { get; init; }

    /// <summary>json | form | multipart | query | empty | invalid — how the body was interpreted.</summary>
    public string BodyFormat { get; init; } = "empty";
    public int BodyLength { get; init; }

    /// <summary>First 4 KB of the body in ≤1000-character chunks (the logging redactor bounds single strings at 1000).</summary>
    public IReadOnlyList<string> BodyPreview { get; init; } = [];

    /// <summary>Keys that were present but not needed for the selected mode (e.g. <c>limit</c>, <c>size</c>).</summary>
    public IReadOnlyList<string> IgnoredKeys { get; init; } = [];

    public bool Succeeded => Request is not null && Error is null;
}

public interface ITorobRequestParser
{
    Task<TorobRequestParseResult> ParseAsync(HttpRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the Torob products request as leniently as the crawler could plausibly send it: JSON regardless of
/// Content-Type, form-urlencoded, multipart or query-string bodies, stringified or floating numbers, Persian
/// digits, PHP-style <c>key[]</c> names and the singular v2 names. Unknown keys such as <c>limit</c>/<c>size</c>
/// are ignored and reported, never rejected. Only the cases Torob's document defines as errors yield a 400.
/// </summary>
public sealed class TorobRequestParser : ITorobRequestParser
{
    public const int PreviewChars = 4096;
    private const int PreviewChunk = 1000;
    private const int RedactionWindowChars = 65_536;

    public const string EmptyBodyError = "بدنه درخواست الزامی است.";
    public const string InvalidJsonError = "بدنه درخواست باید یک شیء JSON معتبر باشد.";
    public const string InvalidFormError = "بدنه فرم ارسال‌شده قابل خواندن نیست.";
    public const string InvalidListError = "فهرست درخواست معتبر نیست.";
    public const string InvalidCursorError = "cursor باید همان next_cursor پاسخ قبلی باشد.";
    public const string CursorWithoutSortError = "cursor فقط همراه sort برابر product_id_desc مجاز است.";
    public const string PageNotIntegerError = "page باید یک عدد صحیح باشد.";
    public const string PageBelowOneError = "page باید از ۱ شروع شود.";
    public const string SortMissingError = "sort الزامی است؛ date_added_desc یا date_updated_desc.";
    public const string SortInvalidError = "sort باید date_added_desc یا date_updated_desc باشد.";
    public const string NoModeError = "دقیقاً یکی از page، page_urls یا page_uniques باید ارسال شود.";

    private const string PageUrlsKey = "page_urls";
    private const string PageUniquesKey = "page_uniques";

    public async Task<TorobRequestParseResult> ParseAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        string raw;
        using (var reader = new StreamReader(request.Body, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
            raw = await reader.ReadToEndAsync(cancellationToken);
        if (request.Body.CanSeek) request.Body.Position = 0;

        var text = raw.Trim();
        if (text.Length == 0 && request.HasFormContentType)
        {
            // Something earlier in the pipeline (e.g. MVC's form value provider) may already have consumed
            // a form body; the parsed form stays cached on the request, so read it from there.
            return await FromFormAsync(request, preview: null, length: (int)Math.Min(request.ContentLength ?? 0, int.MaxValue), cancellationToken);
        }

        var preview = Chunk(text);
        if (text.StartsWith('{')) return FromJson(text, "json", preview);
        if (text.Length == 0) return Fail(EmptyBodyError, "empty", preview, 0);
        if (request.HasFormContentType) return await FromFormAsync(request, preview, text.Length, cancellationToken);
        if (LooksLikeQueryString(text)) return FromFields(QueryHelpers.ParseQuery(text), "query", preview, text.Length);

        return FromJson(text, "invalid", preview);
    }

    private static async Task<TorobRequestParseResult> FromFormAsync(
        HttpRequest request,
        IReadOnlyList<string>? preview,
        int length,
        CancellationToken cancellationToken)
    {
        var format = request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) == true ? "multipart" : "form";
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            if (form.Count == 0) return Fail(EmptyBodyError, "empty", preview ?? [], length);

            // A JSON body mislabelled as a form arrives as a single key with no value.
            if (form.Count == 1 && form.Keys.First() is { } onlyKey && onlyKey.TrimStart().StartsWith('{') &&
                string.IsNullOrEmpty(form[onlyKey].ToString()))
                return FromJson(onlyKey.Trim(), "json", preview ?? Chunk(onlyKey.Trim()));

            if (preview is null)
            {
                var rendered = string.Join("&", form.Select(pair => $"{pair.Key}={string.Join(",", pair.Value.ToArray())}"));
                preview = Chunk(rendered);
                if (length == 0) length = rendered.Length;
            }
            return FromFields(form, format, preview, length);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or IOException)
        {
            return Fail(InvalidFormError, "invalid", preview ?? [], length);
        }
    }

    public static TorobRequestParseResult FromJson(string text, string format, IReadOnlyList<string> preview)
    {
        var fields = new Dictionary<string, Loose>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Fail(InvalidJsonError, format, preview, text.Length);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                // Everything is copied to strings inside the using block; JsonElement never escapes.
                var loose = ToLoose(property.Value);
                if (loose is not null) fields[property.Name.Trim()] = loose;
            }
        }
        catch (JsonException)
        {
            return Fail(InvalidJsonError, format, preview, text.Length);
        }

        return Resolve(fields, format, preview, text.Length);
    }

    public static TorobRequestParseResult FromFields(
        IEnumerable<KeyValuePair<string, StringValues>> pairs,
        string format,
        IReadOnlyList<string> preview,
        int length)
    {
        var fields = new Dictionary<string, Loose>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rawKey, values) in pairs)
        {
            var key = rawKey.Trim();
            if (key.EndsWith("[]", StringComparison.Ordinal)) key = key[..^2];
            if (key.Equals("page_url", StringComparison.OrdinalIgnoreCase)) key = PageUrlsKey;
            else if (key.Equals("page_unique", StringComparison.OrdinalIgnoreCase)) key = PageUniquesKey;

            var items = values.Where(value => value is not null).Select(value => value!).ToList();
            if (items.Count == 0) continue;

            if (key.Equals(PageUrlsKey, StringComparison.OrdinalIgnoreCase) ||
                key.Equals(PageUniquesKey, StringComparison.OrdinalIgnoreCase))
            {
                var list = new List<string>();
                foreach (var item in items)
                {
                    var trimmed = item.Trim();
                    if (trimmed.StartsWith('[') && TryParseJsonStringArray(trimmed, out var parsed)) list.AddRange(parsed);
                    else list.Add(item);
                }
                if (fields.TryGetValue(key, out var existing) && existing.List is not null) existing.List.AddRange(list);
                else fields[key] = new Loose(null, list, false);
            }
            else
            {
                fields[key] = new Loose(items[0], null, false);
            }
        }

        return Resolve(fields, format, preview, length);
    }

    private static TorobRequestParseResult Resolve(
        Dictionary<string, Loose> fields,
        string format,
        IReadOnlyList<string> preview,
        int length)
    {
        TorobRequestParseResult Ok(TorobProductsRequest request, TorobRequestMode mode, params string[] used) => new()
        {
            Request = request,
            Mode = mode,
            BodyFormat = format,
            BodyLength = length,
            BodyPreview = preview,
            IgnoredKeys = fields.Keys
                .Where(key => !used.Contains(key, StringComparer.OrdinalIgnoreCase))
                .Select(key => key.ToLowerInvariant())
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray()
        };
        TorobRequestParseResult Bad(string error) => Fail(error, format, preview, length);

        // Explicit lookups win over everything else; Torob never mixes modes, so extra keys are just ignored.
        if (TryList(fields, PageUrlsKey, out var urls, out var urlsMalformed))
            return urlsMalformed ? Bad(InvalidListError) : Ok(new TorobProductsRequest { PageUrls = urls }, TorobRequestMode.PageUrls, PageUrlsKey);
        if (TryList(fields, PageUniquesKey, out var uniques, out var uniquesMalformed))
            return uniquesMalformed ? Bad(InvalidListError) : Ok(new TorobProductsRequest { PageUniques = uniques }, TorobRequestMode.PageUniques, PageUniquesKey);

        var sort = Scalar(fields, "sort");
        var cursor = Scalar(fields, "cursor");
        // A cursor key that is present but unusable (empty text, an object, an array) must not silently fall
        // back to the first page: Torob forbids defaults, and a silent restart would loop the crawler.
        // A JSON null is the one accepted spelling of "no cursor", i.e. the first page.
        var cursorUnusable = fields.ContainsKey("cursor") && cursor is null;
        if (string.Equals(sort, "product_id_desc", StringComparison.OrdinalIgnoreCase))
        {
            if (cursorUnusable || (cursor is not null && !TorobCursor.TryDecode(cursor, out _))) return Bad(InvalidCursorError);
            return Ok(new TorobProductsRequest { Sort = "product_id_desc", Cursor = cursor }, TorobRequestMode.Cursor, "sort", "cursor");
        }
        if (cursor is not null || cursorUnusable)
            return Bad(sort is null ? CursorWithoutSortError : InvalidCursorError);

        if (fields.TryGetValue("page", out var pageField))
        {
            if (pageField.List is not null || pageField.Malformed || !TryCoerceInt(pageField.Scalar, out var page))
                return Bad(PageNotIntegerError);
            if (page < 1) return Bad(PageBelowOneError);
            if (sort is null) return Bad(SortMissingError);
            var canonicalSort = sort.ToLowerInvariant();
            if (canonicalSort is not ("date_added_desc" or "date_updated_desc")) return Bad(SortInvalidError);
            return Ok(new TorobProductsRequest { Page = page, Sort = canonicalSort }, TorobRequestMode.Page, "page", "sort");
        }

        return Bad(NoModeError);
    }

    private static bool TryList(Dictionary<string, Loose> fields, string key, out List<string> values, out bool malformed)
    {
        values = [];
        malformed = false;
        if (!fields.TryGetValue(key, out var loose)) return false;

        malformed = loose.Malformed;
        var source = loose.List ?? (loose.Scalar is null ? [] : [loose.Scalar]);
        values = source
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        if (values.Count == 0) malformed = true;
        return true;
    }

    private static string? Scalar(Dictionary<string, Loose> fields, string key)
    {
        if (!fields.TryGetValue(key, out var loose)) return null;
        var value = loose.Scalar ?? loose.List?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Loose? ToLoose(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => new Loose(value.GetString(), null, false),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => new Loose(value.GetRawText(), null, false),
        JsonValueKind.Array => ArrayToLoose(value),
        _ => new Loose(null, null, true)
    };

    private static Loose ArrayToLoose(JsonElement array)
    {
        var list = new List<string>();
        var malformed = false;
        foreach (var item in array.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    list.Add(item.GetString()!);
                    break;
                case JsonValueKind.Number:
                    list.Add(item.GetRawText());
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    malformed = true;
                    break;
            }
        }
        return new Loose(null, list, malformed);
    }

    private static bool TryParseJsonStringArray(string text, out List<string> values)
    {
        values = [];
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
            var loose = ArrayToLoose(document.RootElement);
            if (loose.Malformed) return false;
            values = loose.List!;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryCoerceInt(string? value, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = NormalizeDigits(value.Trim());
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) return true;
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false;
        if (number != decimal.Truncate(number) || number < int.MinValue || number > int.MaxValue) return false;
        result = (int)number;
        return true;
    }

    /// <summary>Maps Persian (U+06F0–U+06F9) and Arabic-Indic (U+0660–U+0669) digits to ASCII.</summary>
    private static string NormalizeDigits(string value)
    {
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (character is >= '۰' and <= '۹') characters[index] = (char)('0' + (character - '۰'));
            else if (character is >= '٠' and <= '٩') characters[index] = (char)('0' + (character - '٠'));
        }
        return new string(characters);
    }

    private static bool LooksLikeQueryString(string text)
    {
        var equals = text.IndexOf('=');
        if (equals <= 0 || text.Contains('{')) return false;
        for (var index = 0; index < equals; index++)
        {
            var character = text[index];
            if (!(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '[' or ']' or '%' or '.')) return false;
        }
        return true;
    }

    /// <summary>
    /// Bounded, redacted preview for the diagnostic log. Redaction runs on a window much larger than the
    /// preview and only then is the text cut, so a sensitive pair that straddles the 4 KB boundary is
    /// redacted before it can be truncated; if the cut still lands inside a JSON string, that partial
    /// value is dropped entirely. Chunking happens last so nothing is split across per-string redaction.
    /// </summary>
    private static IReadOnlyList<string> Chunk(string text)
    {
        var window = text.Length > RedactionWindowChars ? text[..RedactionWindowChars] : text;
        var redacted = SensitiveLogData.RedactFreeText(window, Math.Max(1, window.Length));
        var bounded = redacted.Length > PreviewChars ? CutOutsideJsonStrings(redacted[..PreviewChars]) : redacted;
        var chunks = new List<string>((bounded.Length + PreviewChunk - 1) / PreviewChunk);
        for (var offset = 0; offset < bounded.Length; offset += PreviewChunk)
            chunks.Add(bounded.Substring(offset, Math.Min(PreviewChunk, bounded.Length - offset)));
        return chunks;
    }

    /// <summary>If the preview was cut inside a JSON string, remove that partial value: its key is unknown at the cut.</summary>
    private static string CutOutsideJsonStrings(string text)
    {
        var inString = false;
        var escaped = false;
        var opening = -1;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') inString = false;
            }
            else if (character == '"')
            {
                inString = true;
                opening = index;
            }
        }
        return inString && opening >= 0 ? text[..(opening + 1)] + "…" : text;
    }

    private static TorobRequestParseResult Fail(string error, string format, IReadOnlyList<string> preview, int length) => new()
    {
        Error = error,
        Mode = TorobRequestMode.Invalid,
        BodyFormat = format,
        BodyLength = length,
        BodyPreview = preview
    };

    /// <summary>A loosely typed request field: a scalar text, a list of texts, or something unusable.</summary>
    private sealed record Loose(string? Scalar, List<string>? List, bool Malformed);
}
