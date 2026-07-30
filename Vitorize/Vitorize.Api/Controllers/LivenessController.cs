using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Vitorize.Api.Controllers;

/// <summary>
/// A process-only probe for load balancers. It must stay free of database, cache,
/// payment and configuration dependencies so a transient dependency outage does not
/// cause the host to be restarted unnecessarily.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/health/live")]
[SwaggerTag("Process liveness endpoint for load balancers.")]
public sealed class LivenessController : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Process liveness", Description = "Confirms that the API process can serve requests without checking dependencies.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Check() => Ok(new { Status = "Healthy" });
}
