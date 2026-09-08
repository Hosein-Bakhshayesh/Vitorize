using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Vitorize.Api.Controllers;
using Vitorize.Api.Services;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Application.Interfaces;
using Xunit;

namespace Vitorize.Tests;

public sealed class TorobControllerTests
{
    [Fact]
    public void Uses_the_documented_C_Torob_token_version_header()
    {
        var authenticator = new TorobRequestAuthenticator(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        var request = new DefaultHttpContext().Request;
        request.Headers["C-Torob-Token-Version"] = "1";

        authenticator.TryValidate(request, out var error).Should().BeFalse();
        error.Should().Be("توکن ترب ارسال نشده است.");
    }

    [Fact]
    public async Task Valid_paged_request_returns_the_raw_torob_contract()
    {
        var expected = new TorobProductsResponse { CurrentPage = 1, Total = 0, MaxPages = 1 };
        var controller = new TorobController(
            new StubCatalog(expected),
            new AcceptingAuthenticator())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Products(new TorobProductsRequest
        {
            Page = 1,
            Sort = "date_added_desc"
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Rejects_request_that_mixes_torob_lookup_modes()
    {
        var controller = new TorobController(
            new StubCatalog(new TorobProductsResponse()),
            new AcceptingAuthenticator())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Products(new TorobProductsRequest
        {
            Page = 1,
            Sort = "date_added_desc",
            PageUniques = ["variant:one"]
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private sealed class StubCatalog(TorobProductsResponse response) : ITorobCatalogService
    {
        public Task<TorobProductsResponse> GetProductsAsync(TorobProductsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class AcceptingAuthenticator : ITorobRequestAuthenticator
    {
        public bool TryValidate(HttpRequest request, out string error)
        {
            error = string.Empty;
            return true;
        }
    }
}
