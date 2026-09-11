using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Api.Controllers;
using Vitorize.Api.Services;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Application.Interfaces;
using Xunit;

namespace Vitorize.Tests;

public sealed class TorobControllerTests
{
    [Fact]
    public async Task Valid_paged_json_returns_the_raw_torob_contract()
    {
        var expected = new TorobProductsResponse { CurrentPage = 1, Total = 0, MaxPages = 1 };
        var catalog = new StubCatalog(expected);
        var controller = CreateController(catalog, "{\"page\": 1, \"sort\": \"date_added_desc\"}");

        var result = await controller.Products();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        catalog.Received!.Page.Should().Be(1);
        catalog.Received.Sort.Should().Be("date_added_desc");
    }

    [Fact]
    public async Task Explicit_lookup_takes_precedence_over_page_and_sort()
    {
        var catalog = new StubCatalog(new TorobProductsResponse());
        var controller = CreateController(catalog, "{\"page\":1,\"sort\":\"date_added_desc\",\"page_uniques\":[\"variant:one\"]}");

        var result = await controller.Products();

        result.Should().BeOfType<OkObjectResult>();
        catalog.Received!.PageUniques.Should().Equal("variant:one");
        catalog.Received.Page.Should().BeNull();
        catalog.Received.Sort.Should().BeNull();
    }

    [Fact]
    public async Task Empty_body_returns_only_the_documented_error()
    {
        var catalog = new StubCatalog(new TorobProductsResponse());
        var controller = CreateController(catalog, "");

        var result = await controller.Products();

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeOfType<TorobErrorResponse>().Which.Error.Should().NotBeNullOrWhiteSpace();
        catalog.Received.Should().BeNull();
    }

    [Fact]
    public async Task Missing_token_is_accepted_unless_enforcement_is_enabled()
    {
        const string body = "{\"page\":1,\"sort\":\"date_added_desc\"}";
        var lenient = CreateController(new StubCatalog(new TorobProductsResponse()), body, includeToken: false);
        (await lenient.Products()).Should().BeOfType<OkObjectResult>();

        var strict = CreateController(new StubCatalog(new TorobProductsResponse()), body, includeToken: false,
            configuration: Configuration(("Torob:EnforceToken", "true")));
        var rejected = await strict.Products();
        rejected.Should().BeOfType<UnauthorizedObjectResult>().Which.Value.Should().BeOfType<TorobErrorResponse>();
    }

    private static TorobController CreateController(
        StubCatalog catalog,
        string body,
        string? contentType = "application/json",
        bool includeToken = true,
        IConfiguration? configuration = null)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        if (contentType is not null) context.Request.ContentType = contentType;
        if (includeToken) context.Request.Headers["X-Torob-Token"] = "non-secret-test-value";
        return new TorobController(
            catalog,
            new TorobRequestParser(),
            new TorobRequestAuthenticator(configuration ?? Configuration()),
            NullLogger<TorobController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();

    private sealed class StubCatalog(TorobProductsResponse response) : ITorobCatalogService
    {
        public TorobProductsRequest? Received { get; private set; }

        public Task<TorobProductsResponse> GetProductsAsync(TorobProductsRequest request, CancellationToken cancellationToken = default)
        {
            Received = request;
            return Task.FromResult(response);
        }
    }
}
