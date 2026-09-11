namespace Vitorize.Api.Services;

/// <summary>
/// One structured log line per Torob request so the crawler's real body, headers and address can be read
/// from the API log when Torob reports a rejection. Property names deliberately avoid the words that
/// <c>SensitiveLogData</c> redacts (Token, Key, Authorization, Secret, Cookie, …); the token value itself is
/// never logged — only its length and decoded claims. The body preview is chunked because the redactor
/// bounds each string at 1000 characters.
/// </summary>
public static class TorobRequestLog
{
    public const string EventType = "TorobProductsRequest";

    /// <summary>Single-line template; every placeholder name must stay clear of SensitiveLogData's redaction list.</summary>
    public const string MessageTemplate =
        "Torob products request {Mode} responded {StatusCode} in {ElapsedMs} ms. " +
        "RemoteIp={RemoteIp} ForwardedFor={ForwardedFor} OriginalFor={OriginalFor} Host={HostHeader} ForwardedHost={ForwardedHost} " +
        "Method={Method} UserAgent={UserAgent} ContentType={ContentType} ContentLength={ContentLength} HeaderNames={HeaderNames} " +
        "AuthHeaderPresent={AuthHeaderPresent} AuthHeaderLength={AuthHeaderLength} JwtShaped={JwtShaped} JwtAlgorithm={JwtAlgorithm} " +
        "JwtHeaderVersion={JwtHeaderVersion} JwtAudiences={JwtAudiences} JwtAudienceMatches={JwtAudienceMatches} JwtExpiresAt={JwtExpiresAt} " +
        "JwtNotBefore={JwtNotBefore} JwtTimeValid={JwtTimeValid} JwtSignatureValid={JwtSignatureValid} JwtParseError={JwtParseError} " +
        "VersionHeader={VersionHeader} BodyFormat={BodyFormat} BodyLength={BodyLength} IgnoredKeys={IgnoredKeys} ProductCount={ProductCount} " +
        "ErrorText={ErrorText} Body={BodyPreview} EventType={EventType}";

    public static void Write(
        ILogger logger,
        HttpContext context,
        TorobTokenInfo token,
        TorobRequestParseResult parsed,
        int statusCode,
        string? error,
        int? productCount,
        TimeSpan elapsed)
    {
        var level = statusCode == StatusCodes.Status200OK && token.Present ? LogLevel.Information : LogLevel.Warning;
        if (!logger.IsEnabled(level)) return;

        var request = context.Request;
        object?[] values =
        [
            parsed.Mode,
            statusCode,
            Math.Round(elapsed.TotalMilliseconds, 1),
            context.Connection.RemoteIpAddress?.ToString(),
            HeaderOrNull(request, "X-Forwarded-For"),
            HeaderOrNull(request, "X-Original-For"),
            request.Host.Value,
            HeaderOrNull(request, "X-Forwarded-Host"),
            request.Method,
            HeaderOrNull(request, "User-Agent"),
            request.ContentType,
            request.ContentLength,
            request.Headers.Keys.ToArray(),
            token.Present,
            token.Length,
            token.LooksLikeJwt,
            token.Algorithm,
            token.HeaderVersion,
            token.Audiences,
            token.AudienceMatches,
            token.ExpiresAt,
            token.NotBefore,
            token.TimeValid,
            token.SignatureValid,
            token.ParseError,
            token.VersionHeader,
            parsed.BodyFormat,
            parsed.BodyLength,
            parsed.IgnoredKeys,
            productCount,
            error,
            parsed.BodyPreview,
            EventType
        ];

        logger.Log(level, MessageTemplate, values);
    }

    private static string? HeaderOrNull(HttpRequest request, string name) =>
        request.Headers.TryGetValue(name, out var values) && !string.IsNullOrWhiteSpace(values.ToString()) ? values.ToString() : null;
}
