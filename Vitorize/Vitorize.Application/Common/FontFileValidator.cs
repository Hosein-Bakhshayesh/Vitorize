using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

public static class FontFileValidator
{
    public static readonly string[] Extensions = [".woff2", ".woff", ".ttf"];
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    public static string Validate(string extension, string contentType, long length, ReadOnlySpan<byte> header, long maxBytes = DefaultMaxBytes)
    {
        extension = (extension ?? string.Empty).ToLowerInvariant();
        if (!Extensions.Contains(extension)) throw new BusinessException("فرمت فونت باید WOFF2، WOFF یا TTF باشد.");
        if (length <= 0 || length > maxBytes) throw new BusinessException($"حجم فونت نباید بیشتر از {maxBytes / 1024 / 1024} مگابایت باشد.");
        var mime = (contentType ?? string.Empty).ToLowerInvariant();
        var mimeOk = extension switch
        {
            ".woff2" => mime is "font/woff2" or "application/font-woff2" or "application/octet-stream",
            ".woff" => mime is "font/woff" or "application/font-woff" or "application/octet-stream",
            _ => mime is "font/ttf" or "application/x-font-ttf" or "application/octet-stream"
        };
        if (!mimeOk) throw new BusinessException("نوع محتوای فایل فونت معتبر نیست.");
        var signatureOk = extension switch
        {
            ".woff2" => header.Length >= 4 && header[..4].SequenceEqual("wOF2"u8),
            ".woff" => header.Length >= 4 && header[..4].SequenceEqual("wOFF"u8),
            _ => header.Length >= 4 &&
                 (header[..4].SequenceEqual(new byte[] { 0, 1, 0, 0 }) || header[..4].SequenceEqual("true"u8) || header[..4].SequenceEqual("typ1"u8))
        };
        if (!signatureOk) throw new BusinessException("امضای باینری فایل با فرمت فونت مطابقت ندارد.");
        return extension[1..];
    }
}
