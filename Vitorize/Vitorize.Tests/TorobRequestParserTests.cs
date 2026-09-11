using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Vitorize.Api.Services;
using Vitorize.Application.DTOs.Torob;
using Xunit;

namespace Vitorize.Tests;

public sealed class TorobRequestParserTests
{
    [Theory]
    [InlineData("{\"page\":1,\"sort\":\"date_added_desc\"}", 1)]
    [InlineData("{\"page\":\"1\",\"sort\":\"date_added_desc\"}", 1)]
    [InlineData("{\"page\":2.0,\"sort\":\"date_added_desc\"}", 2)]
    [InlineData("{\"page\":\"3.0\",\"sort\":\"date_added_desc\"}", 3)]
    [InlineData("{\"page\":1e0,\"sort\":\"date_added_desc\"}", 1)]
    [InlineData("{\"page\":\"۴\",\"sort\":\"date_added_desc\"}", 4)]
    [InlineData("{\"Page\":5,\"SORT\":\"Date_Added_Desc\"}", 5)]
    public async Task Page_is_coerced_from_numbers_strings_and_persian_digits(string body, int expectedPage)
    {
        var result = await Parse(body);

        result.Succeeded.Should().BeTrue(result.Error);
        result.Mode.Should().Be(TorobRequestMode.Page);
        result.Request!.Page.Should().Be(expectedPage);
        result.Request.Sort.Should().Be("date_added_desc");
        result.BodyFormat.Should().Be("json");
    }

    [Theory]
    [InlineData("{\"page\":\"abc\",\"sort\":\"date_added_desc\"}", TorobRequestParser.PageNotIntegerError)]
    [InlineData("{\"page\":1.5,\"sort\":\"date_added_desc\"}", TorobRequestParser.PageNotIntegerError)]
    [InlineData("{\"page\":true,\"sort\":\"date_added_desc\"}", TorobRequestParser.PageNotIntegerError)]
    [InlineData("{\"page\":[1],\"sort\":\"date_added_desc\"}", TorobRequestParser.PageNotIntegerError)]
    [InlineData("{\"page\":0,\"sort\":\"date_added_desc\"}", TorobRequestParser.PageBelowOneError)]
    [InlineData("{\"page\":1}", TorobRequestParser.SortMissingError)]
    [InlineData("{\"page\":1,\"sort\":\"\"}", TorobRequestParser.SortMissingError)]
    [InlineData("{\"page\":1,\"sort\":null}", TorobRequestParser.SortMissingError)]
    [InlineData("{\"page\":1,\"sort\":\"unknown\"}", TorobRequestParser.SortInvalidError)]
    [InlineData("{}", TorobRequestParser.NoModeError)]
    [InlineData("{\"limit\":100}", TorobRequestParser.NoModeError)]
    [InlineData("{\"sort\":\"date_added_desc\"}", TorobRequestParser.NoModeError)]
    [InlineData("", TorobRequestParser.EmptyBodyError)]
    [InlineData("   ", TorobRequestParser.EmptyBodyError)]
    [InlineData("{", TorobRequestParser.InvalidJsonError)]
    [InlineData("[]", TorobRequestParser.InvalidJsonError)]
    [InlineData("null", TorobRequestParser.InvalidJsonError)]
    [InlineData("text", TorobRequestParser.InvalidJsonError)]
    [InlineData("{\"page_urls\":[]}", TorobRequestParser.InvalidListError)]
    [InlineData("{\"page_uniques\":[null]}", TorobRequestParser.InvalidListError)]
    [InlineData("{\"page_uniques\":[true]}", TorobRequestParser.InvalidListError)]
    [InlineData("{\"page_uniques\":{\"a\":1}}", TorobRequestParser.InvalidListError)]
    [InlineData("{\"sort\":\"product_id_desc\",\"cursor\":\"invalid\"}", TorobRequestParser.InvalidCursorError)]
    [InlineData("{\"cursor\":\"invalid\"}", TorobRequestParser.CursorWithoutSortError)]
    [InlineData("{\"sort\":\"product_id_desc\",\"cursor\":{}}", TorobRequestParser.InvalidCursorError)]
    public async Task Documented_errors_are_reported_with_their_message(string body, string expectedError)
    {
        var result = await Parse(body);

        result.Succeeded.Should().BeFalse();
        result.Mode.Should().Be(TorobRequestMode.Invalid);
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public async Task Extra_keys_are_ignored_and_reported()
    {
        var result = await Parse("{\"page\":1,\"sort\":\"date_added_desc\",\"limit\":100,\"size\":100,\"shop\":\"x\"}");

        result.Succeeded.Should().BeTrue(result.Error);
        result.IgnoredKeys.Should().Equal("limit", "shop", "size");
    }

    [Fact]
    public async Task Explicit_lookups_take_precedence_and_accept_scalars_numbers_and_blank_siblings()
    {
        var mixed = await Parse("{\"page\":1,\"sort\":\"date_added_desc\",\"page_urls\":[\"https://a/x\"],\"page_uniques\":[\"u\"]}");
        mixed.Mode.Should().Be(TorobRequestMode.PageUrls);
        mixed.Request!.PageUrls.Should().Equal("https://a/x");
        mixed.Request.Page.Should().BeNull();
        mixed.IgnoredKeys.Should().Equal("page", "page_uniques", "sort");

        var scalar = await Parse("{\"page_urls\":\"https://a/x\"}");
        scalar.Request!.PageUrls.Should().Equal("https://a/x");

        var numbers = await Parse("{\"page_uniques\":[42, \" b \", null]}");
        numbers.Mode.Should().Be(TorobRequestMode.PageUniques);
        numbers.Request!.PageUniques.Should().Equal("42", "b");

        var blankSiblings = await Parse("{\"page_uniques\":[\"one\"],\"sort\":\"\",\"cursor\":null}");
        blankSiblings.Succeeded.Should().BeTrue(blankSiblings.Error);
    }

    [Fact]
    public async Task Cursor_mode_ignores_page_limit_and_size_and_accepts_a_cursor_without_sort()
    {
        var first = await Parse("{\"sort\":\"PRODUCT_ID_DESC\",\"page\":1,\"limit\":100,\"size\":100}");
        first.Mode.Should().Be(TorobRequestMode.Cursor);
        first.Request!.Sort.Should().Be("product_id_desc");
        first.Request.Cursor.Should().BeNull();
        first.IgnoredKeys.Should().Equal("limit", "page", "size");

        var cursor = new TorobCursor("variant:abc", 2).Encode();
        var next = await Parse($"{{\"sort\":\"product_id_desc\",\"cursor\":\"{cursor}\"}}");
        next.Request!.Cursor.Should().Be(cursor);

        // Torob forbids defaults: a cursor without its sort, or a cursor that is not text, is an error
        // rather than a silent restart from page one.
        var withoutSort = await Parse($"{{\"cursor\":\"{cursor}\"}}");
        withoutSort.Error.Should().Be(TorobRequestParser.CursorWithoutSortError);
        var objectCursor = await Parse("{\"sort\":\"product_id_desc\",\"cursor\":{}}");
        objectCursor.Error.Should().Be(TorobRequestParser.InvalidCursorError);
        var arrayCursor = await Parse("{\"sort\":\"product_id_desc\",\"cursor\":[1]}");
        arrayCursor.Error.Should().Be(TorobRequestParser.InvalidCursorError);
        var emptyCursor = await Parse("{\"sort\":\"product_id_desc\",\"cursor\":\"\"}");
        emptyCursor.Error.Should().Be(TorobRequestParser.InvalidCursorError);
        var blankCursor = await Parse("{\"sort\":\"product_id_desc\",\"cursor\":\"   \"}");
        blankCursor.Error.Should().Be(TorobRequestParser.InvalidCursorError);
        var emptyFormCursor = await Parse("sort=product_id_desc&cursor=", "application/x-www-form-urlencoded");
        emptyFormCursor.Error.Should().Be(TorobRequestParser.InvalidCursorError);
        var nullCursor = await Parse("{\"sort\":\"product_id_desc\",\"cursor\":null}");
        nullCursor.Succeeded.Should().BeTrue(nullCursor.Error);
        nullCursor.Request!.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task Body_preview_does_not_leak_a_secret_that_straddles_the_truncation_boundary()
    {
        const string head = "{\"page\":1,\"sort\":\"date_added_desc\",\"filler\":\"";
        const string tail = "\",\"password\":\"";
        var secret = new string('S', 300);
        // Place the opening of the password value 30 characters before the preview boundary.
        var filler = new string('x', TorobRequestParser.PreviewChars - 30 - head.Length - tail.Length);
        var body = head + filler + tail + secret + "\",\"after\":\"y\"}";

        var result = await Parse(body);

        result.Succeeded.Should().BeTrue(result.Error);
        var preview = string.Concat(result.BodyPreview);
        preview.Length.Should().BeLessThanOrEqualTo(TorobRequestParser.PreviewChars);
        preview.Should().NotContain("SSSSS");
        preview.Should().Contain("\"password\":\"[REDACTED]\"");

        // A value of a harmless key that is cut by the boundary is dropped too, because at the cut its key is unknown.
        var note = new string('n', 300);
        var cutBody = head + filler + "\",\"note\":\"" + note + "\"}";
        var cut = await Parse(cutBody);
        var cutPreview = string.Concat(cut.BodyPreview);
        cutPreview.Should().NotContain("nnnnn");
        cutPreview.Should().EndWith("\"note\":\"…");
    }

    [Fact]
    public async Task Body_preview_redacts_sensitive_json_values_but_keeps_torob_fields()
    {
        var result = await Parse("{\"page\":1,\"sort\":\"date_added_desc\",\"password\":\"demo-value-only\",\"api_key\":\"k-123\",\"Authorization\":\"Bearer abc.def\"}");

        result.Succeeded.Should().BeTrue(result.Error);
        var preview = string.Concat(result.BodyPreview);
        preview.Should().Contain("\"page\":1").And.Contain("\"sort\":\"date_added_desc\"");
        preview.Should().NotContain("demo-value-only").And.NotContain("k-123").And.NotContain("abc.def");
        preview.Should().Contain("\"password\":\"[REDACTED]\"");
    }

    [Fact]
    public async Task Form_multipart_query_and_untyped_bodies_are_accepted()
    {
        var form = await Parse("page=1&sort=date_added_desc", "application/x-www-form-urlencoded");
        form.Succeeded.Should().BeTrue(form.Error);
        form.BodyFormat.Should().Be("form");
        form.Request!.Page.Should().Be(1);

        var lists = await Parse(
            "page_urls[]=https%3A%2F%2Fa%2Fx&page_urls[]=https%3A%2F%2Fa%2Fy&page_url=https%3A%2F%2Fa%2Fz",
            "application/x-www-form-urlencoded");
        lists.Mode.Should().Be(TorobRequestMode.PageUrls);
        lists.Request!.PageUrls.Should().BeEquivalentTo(["https://a/x", "https://a/y", "https://a/z"]);

        var jsonArrayField = await Parse("page_uniques=%5B%22a%22%2C%22b%22%5D", "application/x-www-form-urlencoded");
        jsonArrayField.Request!.PageUniques.Should().Equal("a", "b");

        const string boundary = "torob-boundary";
        var multipartBody =
            $"--{boundary}\r\nContent-Disposition: form-data; name=\"page\"\r\n\r\n1\r\n" +
            $"--{boundary}\r\nContent-Disposition: form-data; name=\"sort\"\r\n\r\ndate_added_desc\r\n" +
            $"--{boundary}--\r\n";
        var multipart = await Parse(multipartBody, $"multipart/form-data; boundary={boundary}");
        multipart.Succeeded.Should().BeTrue(multipart.Error);
        multipart.BodyFormat.Should().Be("multipart");
        multipart.Request!.Page.Should().Be(1);

        var plain = await Parse("{\"page\":1,\"sort\":\"date_added_desc\"}", "text/plain");
        plain.Succeeded.Should().BeTrue(plain.Error);
        plain.BodyFormat.Should().Be("json");

        var untyped = await Parse("{\"page\":1,\"sort\":\"date_added_desc\"}", null);
        untyped.Succeeded.Should().BeTrue(untyped.Error);

        var query = await Parse("page=1&sort=date_added_desc", "text/plain");
        query.Succeeded.Should().BeTrue(query.Error);
        query.BodyFormat.Should().Be("query");

        var mislabeled = await Parse("{\"page\":1,\"sort\":\"date_added_desc\"}", "application/x-www-form-urlencoded");
        mislabeled.Succeeded.Should().BeTrue(mislabeled.Error);
        mislabeled.BodyFormat.Should().Be("json");

        var badForm = await Parse("page=abc&sort=date_added_desc", "application/x-www-form-urlencoded");
        badForm.Error.Should().Be(TorobRequestParser.PageNotIntegerError);
    }

    [Fact]
    public async Task Form_already_consumed_by_the_pipeline_is_read_from_the_cached_form()
    {
        var context = Context("page=2&sort=date_updated_desc&limit=100", "application/x-www-form-urlencoded");
        await context.Request.ReadFormAsync(); // what MVC's form value provider does before the action runs

        var result = await new TorobRequestParser().ParseAsync(context.Request, CancellationToken.None);

        result.Succeeded.Should().BeTrue(result.Error);
        result.BodyFormat.Should().Be("form");
        result.Request!.Page.Should().Be(2);
        result.Request.Sort.Should().Be("date_updated_desc");
        result.IgnoredKeys.Should().Equal("limit");
        result.BodyPreview.Should().ContainSingle().Which.Should().Contain("page=2");
    }

    [Fact]
    public async Task Body_preview_is_bounded_and_chunked_and_the_body_stays_readable()
    {
        // A long numeric array: the 4 KB cut lands between numbers, not inside a JSON string.
        var body = "{\"page\":1,\"sort\":\"date_added_desc\",\"numbers\":[" + string.Concat(Enumerable.Repeat("1,", 3000)) + "1]}";
        var context = Context(body, "application/json");

        var result = await new TorobRequestParser().ParseAsync(context.Request, CancellationToken.None);

        result.Succeeded.Should().BeTrue(result.Error);
        result.BodyLength.Should().Be(body.Length);
        result.BodyPreview.Sum(chunk => chunk.Length).Should().Be(TorobRequestParser.PreviewChars);
        result.BodyPreview.Should().OnlyContain(chunk => chunk.Length <= 1000);
        string.Concat(result.BodyPreview).Should().Be(body[..TorobRequestParser.PreviewChars]);
        context.Request.Body.Position.Should().Be(0);
        using var reader = new StreamReader(context.Request.Body);
        (await reader.ReadToEndAsync()).Should().Be(body);
    }

    private static Task<TorobRequestParseResult> Parse(string body, string? contentType = "application/json") =>
        new TorobRequestParser().ParseAsync(Context(body, contentType).Request, CancellationToken.None);

    private static DefaultHttpContext Context(string body, string? contentType)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        if (contentType is not null) context.Request.ContentType = contentType;
        return context;
    }
}
