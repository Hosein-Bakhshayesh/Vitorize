namespace Vitorize.Web.Services
{
    /// <summary>
    /// مسیر تصاویر آپلودشده روی میزبان API ذخیره می‌شوند؛
    /// این سرویس مسیرهای نسبی را به آدرس کامل قابل نمایش تبدیل می‌کند.
    /// </summary>
    public class MediaUrlResolver
    {
        private readonly string _mediaBaseUrl;

        public MediaUrlResolver(IConfiguration configuration)
        {
            _mediaBaseUrl =
                (configuration["ApiSettings:MediaBaseUrl"] ?? string.Empty)
                    .TrimEnd('/');
        }

        public string Resolve(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim();

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var relative = "/" + path.TrimStart('~', '/');

            // Verification documents are private API resources. Browser image
            // requests cannot attach the server-held bearer token, so route them
            // through the authenticated Web proxy rather than exposing them as
            // direct API media URLs.
            const string verificationDocumentPrefix = "/api/verification/documents/";
            const string verificationDocumentSuffix = "/content";
            if (relative.StartsWith(verificationDocumentPrefix, StringComparison.OrdinalIgnoreCase) &&
                relative.EndsWith(verificationDocumentSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var identifier = relative[verificationDocumentPrefix.Length..^verificationDocumentSuffix.Length];
                if (Guid.TryParse(identifier, out var documentId))
                    return $"/media/verification-documents/{documentId:D}";
            }

            return string.IsNullOrEmpty(_mediaBaseUrl)
                ? relative
                : _mediaBaseUrl + relative;
        }
    }
}
