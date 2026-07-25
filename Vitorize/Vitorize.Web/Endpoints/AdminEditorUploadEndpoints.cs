using Vitorize.Web.Models.Admin.Common;
using Vitorize.Web.Services;

namespace Vitorize.Web.Endpoints
{
    /// <summary>
    /// نقاط آپلود ویرایشگر متن (CKEditor). درخواست چندبخشی از مرورگر ادمین
    /// دریافت و به APIِ آپلود بک‌اند بازارسال می‌شود؛ خروجی در قالب مورد انتظار
    /// CKEditor بازگردانده می‌شود: { url } یا { error: { message } }.
    ///
    /// امنیت: احراز هویت ادمین (کوکی SameSite=Lax)، بررسی هم‌مبدأ بودن درخواست،
    /// و اعتبارسنجی نوع/حجم/امضای فایل روی میزبان API.
    /// </summary>
    public static class AdminEditorUploadEndpoints
    {
        private const long MaxImageBytes = 2 * 1024 * 1024;
        private const long MaxFileBytes = 10 * 1024 * 1024;

        public static void MapAdminEditorUploadEndpoints(this WebApplication app)
        {
            app.MapPost("/admin/editor/upload/image", UploadImageAsync)
                .RequireAuthorization("AdminOnly")
                .DisableAntiforgery();

            app.MapPost("/admin/editor/upload/file", UploadFileAsync)
                .RequireAuthorization("AdminOnly")
                .DisableAntiforgery();
        }

        private static Task<IResult> UploadImageAsync(HttpContext http, ApiClient api, MediaUrlResolver media)
            => ForwardAsync(http, api, media, "admin/uploads/product-image", MaxImageBytes,
                "تصویری ارسال نشده است.");

        private static Task<IResult> UploadFileAsync(HttpContext http, ApiClient api, MediaUrlResolver media)
            => ForwardAsync(http, api, media, "admin/uploads/editor-file", MaxFileBytes,
                "فایلی ارسال نشده است.");

        private static async Task<IResult> ForwardAsync(
            HttpContext http,
            ApiClient api,
            MediaUrlResolver media,
            string apiEndpoint,
            long maxBytes,
            string missingMessage)
        {
            if (!IsSameOrigin(http))
                return Error("درخواست از مبدأ نامعتبر رد شد.", StatusCodes.Status403Forbidden);

            if (!http.Request.HasFormContentType)
                return Error("قالب درخواست نامعتبر است.");

            var form = await http.Request.ReadFormAsync();
            // CKEditor's upload adapter posts the field as "upload"; the attach
            // helper posts "file". Accept whichever arrived.
            var file = form.Files["upload"] ?? form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
                return Error(missingMessage);

            if (file.Length > maxBytes)
                return Error($"حجم فایل نباید بیشتر از {maxBytes / (1024 * 1024)} مگابایت باشد.");

            await using var stream = file.OpenReadStream();
            var result = await api.UploadAsync<UploadResultModel>(
                apiEndpoint, stream, file.FileName, file.ContentType);

            if (!result.IsSuccess || result.Data is null || string.IsNullOrWhiteSpace(result.Data.FilePath))
            {
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? "آپلود فایل ناموفق بود."
                    : result.Message;
                return Error(message);
            }

            var url = media.Resolve(result.Data.FilePath);
            return Results.Json(new { url });
        }

        // Reject cross-origin posts as defence-in-depth on top of the SameSite=Lax
        // admin cookie. A missing Origin (same-origin navigations may omit it) is allowed.
        private static bool IsSameOrigin(HttpContext http)
        {
            var origin = http.Request.Headers.Origin.ToString();
            if (string.IsNullOrWhiteSpace(origin))
                return true;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                return false;

            return string.Equals(originUri.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
                && originUri.Port == (http.Request.Host.Port ?? (http.Request.IsHttps ? 443 : 80));
        }

        private static IResult Error(string message, int statusCode = StatusCodes.Status400BadRequest) =>
            Results.Json(new { error = new { message } }, statusCode: statusCode);
    }
}
