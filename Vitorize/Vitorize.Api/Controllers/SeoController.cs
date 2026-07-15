using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Seo;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers;

[ApiController, Route("api/seo")]
public sealed class SeoController(ISeoService service) : ControllerBase
{
    [HttpGet("sitemap/{kind}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResult<SitemapPageDto>>> Sitemap(string kind, int page = 1, int pageSize = 50_000) =>
        Ok(ApiResult<SitemapPageDto>.Success(await service.GetSitemapAsync(kind, page, pageSize)));

    [HttpGet("redirect")]
    public async Task<ActionResult<ApiResult<LegacyRedirectDto?>>> ResolveRedirect([FromQuery] string path) =>
        Ok(ApiResult<LegacyRedirectDto?>.Success(await service.ResolveRedirectAsync(path)));
}
