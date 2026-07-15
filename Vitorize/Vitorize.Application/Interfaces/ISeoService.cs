using Vitorize.Application.DTOs.Seo;

namespace Vitorize.Application.Interfaces;

public interface ISeoService
{
    Task<SitemapPageDto> GetSitemapAsync(string kind, int page, int pageSize);
    Task<LegacyRedirectDto?> ResolveRedirectAsync(string sourcePath);
}
