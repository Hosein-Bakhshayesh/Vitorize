using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Api.Services;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Application.Interfaces;

namespace Vitorize.Api.Controllers;

/// <summary>
/// Torob API v3 products endpoint. Deliberately NOT an [ApiController]: the body is read and validated by
/// <see cref="ITorobRequestParser"/> so any Content-Type, stringified numbers and extra keys are accepted,
/// and every error is the documented <c>{"error": "..."}</c> with HTTP 400 — never 415 or ProblemDetails.
/// The response is deliberately not wrapped in Vitorize ApiResult.
/// </summary>
[AllowAnonymous]
[Route("api/v1/thirdparties/torob")]
public sealed class TorobController : ControllerBase
{
    private readonly ITorobCatalogService _catalog;
    private readonly ITorobRequestParser _parser;
    private readonly ITorobRequestAuthenticator _authenticator;
    private readonly ILogger<TorobController> _logger;

    public TorobController(
        ITorobCatalogService catalog,
        ITorobRequestParser parser,
        ITorobRequestAuthenticator authenticator,
        ILogger<TorobController> logger)
    {
        _catalog = catalog;
        _parser = parser;
        _authenticator = authenticator;
        _logger = logger;
    }

    /// <remarks>
    /// The action deliberately has no parameters: any bound parameter (even a CancellationToken) makes MVC
    /// build its value providers, and the form value provider would consume a form body before the parser
    /// sees it. Cancellation comes from <see cref="HttpContext.RequestAborted"/> instead.
    /// </remarks>
    [HttpPost("products")]
    [Produces("application/json")]
    public async Task<IActionResult> Products()
    {
        var cancellationToken = HttpContext.RequestAborted;
        var started = Stopwatch.GetTimestamp();
        var token = _authenticator.Inspect(Request);
        var parsed = TorobRequestParseResult.None;
        var statusCode = StatusCodes.Status500InternalServerError;
        string? error = null;
        int? productCount = null;
        try
        {
            // Parse before deciding on the token so a rejected request is still fully described in the log.
            parsed = await _parser.ParseAsync(Request, cancellationToken);

            if (_authenticator.ShouldReject(token, out var authenticationError))
            {
                statusCode = StatusCodes.Status401Unauthorized;
                error = authenticationError;
                return Unauthorized(new TorobErrorResponse { Error = error });
            }

            if (!parsed.Succeeded)
            {
                statusCode = StatusCodes.Status400BadRequest;
                error = parsed.Error ?? TorobRequestParser.EmptyBodyError;
                return BadRequest(new TorobErrorResponse { Error = error });
            }

            var response = await _catalog.GetProductsAsync(parsed.Request!, cancellationToken);
            statusCode = StatusCodes.Status200OK;
            productCount = response.Products.Count;
            return Ok(response);
        }
        finally
        {
            TorobRequestLog.Write(_logger, HttpContext, token, parsed, statusCode, error, productCount, Stopwatch.GetElapsedTime(started));
        }
    }
}
