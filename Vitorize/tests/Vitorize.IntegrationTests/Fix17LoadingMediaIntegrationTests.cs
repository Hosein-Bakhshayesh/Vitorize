using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Api.Hosting;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-17 — the administrator-configurable initial loading image/GIF.
///
/// Two things are proven here. First, that the upload endpoint accepts an animated GIF and that
/// GIF remains rejected everywhere else, so widening the loader did not widen the whole upload
/// surface. Second, that the render-time path rule only ever accepts a genuinely uploaded file:
/// its value is written straight into the boot HTML, so anything else must fall back to default.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix17LoadingMediaIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix17LoadingMediaIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    /// <summary>Smallest valid GIF89a: header, logical screen descriptor and trailer.</summary>
    private static byte[] AnimatedGif() =>
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // "GIF89a"
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
        0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x21, 0xF9, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
    ];

    private static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Loading_media_endpoint_accepts_an_animated_gif_and_stores_it_under_settings()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart("../../loader anim.gif", "image/gif", AnimatedGif());

        var response = await client.PostAsync("/api/admin/uploads/loading-media", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = (await response.Content.ReadFromJsonAsync<ApiResult<UploadFileResultDto>>())!.Data!;
        // The traversal and the space in the client-supplied name must not survive.
        result.FileName.Should().MatchRegex("^[a-f0-9]{32}\\.gif$");
        result.FilePath.Should().Be($"/uploads/settings/{result.FileName}");

        // The stored path must be one the renderer will actually accept, or the upload silently
        // does nothing and the default loader shows instead.
        LoadingMediaRules.IsSafePath(result.FilePath).Should().BeTrue();

        var stored = Path.Combine(PublicMediaRoot(), "settings", result.FileName);
        File.Exists(stored).Should().BeTrue();
        File.Delete(stored);
    }

    [Fact]
    public async Task Loading_media_endpoint_still_accepts_a_still_image()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart("loader.png", "image/png", Png());

        var response = await client.PostAsync("/api/admin/uploads/loading-media", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = (await response.Content.ReadFromJsonAsync<ApiResult<UploadFileResultDto>>())!.Data!;
        File.Delete(Path.Combine(PublicMediaRoot(), "settings", result.FileName));
    }

    /// <summary>Uploads are stored under the API's configured media root, not under wwwroot.</summary>
    private string PublicMediaRoot() =>
        _fixture.Factory.Services.GetRequiredService<HostingStoragePaths>().PublicMediaRoot;

    [Fact]
    public async Task Gif_remains_rejected_by_the_ordinary_settings_image_endpoint()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart("logo.gif", "image/gif", AnimatedGif());

        // Allowing an animated loader must not have widened the general image surface.
        (await client.PostAsync("/api/admin/uploads/settings-image", content)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("payload.gif", "image/gif")]        // .gif extension but the bytes are not a GIF
    [InlineData("payload.svg", "image/svg+xml")]    // SVG can carry script: never allowed
    [InlineData("payload.ico", "image/x-icon")]     // favicon format is not a loader
    [InlineData("payload.exe", "image/gif")]        // executable masquerading behind a GIF MIME
    public async Task Loading_media_endpoint_rejects_spoofed_or_unsafe_files(string fileName, string contentType)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart(fileName, contentType, "not really an image"u8.ToArray());

        (await client.PostAsync("/api/admin/uploads/loading-media", content)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Customer_cannot_upload_loading_media()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart("loader.gif", "image/gif", AnimatedGif());

        (await client.PostAsync("/api/admin/uploads/loading-media", content)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/uploads/settings/abc.gif")]
    [InlineData("/uploads/settings/abc.png")]
    [InlineData("/uploads/settings/abc.jpg")]
    [InlineData("/uploads/settings/abc.jpeg")]
    [InlineData("/uploads/settings/abc.webp")]
    [InlineData("/UPLOADS/SETTINGS/ABC.GIF")]   // case must not matter
    public void Uploaded_settings_media_is_accepted_by_the_render_rule(string path) =>
        LoadingMediaRules.IsSafePath(path).Should().BeTrue();

    [Theory]
    [InlineData(null)]                                          // not configured -> default loader
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.test/x.gif")]                     // absolute URL
    [InlineData("//evil.test/x.gif")]                           // protocol-relative
    [InlineData("data:image/gif;base64,R0lGODlhAQABAAAAACH5")]  // inline payload
    [InlineData("javascript:alert(1)")]
    [InlineData("/uploads/settings/../../web.config")]          // traversal
    [InlineData("/uploads/settings/nested/x.gif")]              // outside the flat settings folder
    [InlineData("/uploads/kyc/x.gif")]                          // a different upload folder
    [InlineData("/uploads/settings/x.svg")]                     // script-capable format
    [InlineData("/uploads/settings/x.ico")]
    [InlineData("/uploads/settings/x.exe")]
    [InlineData("/uploads/settings/x.gif?a=b")]                 // query would ride into the attribute
    [InlineData("/uploads/settings/x.gif#frag")]
    [InlineData("/uploads/settings/")]                          // no file name
    [InlineData("uploads/settings/x.gif")]                      // not rooted
    [InlineData("\\uploads\\settings\\x.gif")]                  // backslashes
    [InlineData("/uploads/settings/x\r\n.gif")]                 // control characters
    public void Unsafe_or_unset_values_fall_back_to_the_default_loader(string? path) =>
        LoadingMediaRules.IsSafePath(path).Should().BeFalse();

    private static MultipartFormDataContent Multipart(string fileName, string contentType, byte[] bytes)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", fileName);
        return multipart;
    }
}
