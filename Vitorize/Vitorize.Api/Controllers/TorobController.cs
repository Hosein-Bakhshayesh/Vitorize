using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Api.Services;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Application.Interfaces;

namespace Vitorize.Api.Controllers;

/// <summary>Torob API v3. The response is deliberately not wrapped in Vitorize ApiResult.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/thirdparties/torob")]
public sealed class TorobController : ControllerBase
{
    private readonly ITorobCatalogService _catalog;
    private readonly ITorobRequestAuthenticator _authenticator;

    public TorobController(ITorobCatalogService catalog, ITorobRequestAuthenticator authenticator)
    {
        _catalog = catalog;
        _authenticator = authenticator;
    }

    [HttpPost("products")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> Products([FromBody] TorobProductsRequest? request, CancellationToken cancellationToken)
    {
        if (!_authenticator.TryValidate(Request, out var authenticationError))
            return Unauthorized(new TorobErrorResponse { Error = authenticationError });

        if (!TryValidateRequest(request, out var validationError))
            return BadRequest(new TorobErrorResponse { Error = validationError });

        return Ok(await _catalog.GetProductsAsync(request!, cancellationToken));
    }

    private static bool TryValidateRequest(TorobProductsRequest? request, out string error)
    {
        error = "";
        if (request is null)
        {
            error = "بدنه درخواست الزامی است.";
            return false;
        }

        var hasPage = request.Page.HasValue;
        var hasUrls = request.PageUrls is { Count: > 0 };
        var hasUniques = request.PageUniques is { Count: > 0 };
        if ((hasPage ? 1 : 0) + (hasUrls ? 1 : 0) + (hasUniques ? 1 : 0) != 1)
        {
            error = "دقیقاً یکی از page، page_urls یا page_uniques باید ارسال شود.";
            return false;
        }

        if (hasPage)
        {
            if (request.Page <= 0)
            {
                error = "page باید از ۱ شروع شود.";
                return false;
            }
            if (request.Sort is not ("date_added_desc" or "date_updated_desc"))
            {
                error = "sort باید date_added_desc یا date_updated_desc باشد.";
                return false;
            }
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.Sort))
        {
            error = "sort فقط همراه page مجاز است.";
            return false;
        }

        var values = hasUrls ? request.PageUrls! : request.PageUniques!;
        if (values.Count > 100 || values.Any(value => string.IsNullOrWhiteSpace(value)))
        {
            error = "فهرست درخواست معتبر نیست.";
            return false;
        }

        return true;
    }
}
