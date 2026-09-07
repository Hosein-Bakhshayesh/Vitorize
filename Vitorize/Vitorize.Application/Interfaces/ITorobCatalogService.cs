using Vitorize.Application.DTOs.Torob;

namespace Vitorize.Application.Interfaces;

/// <summary>Produces the public Torob API v3 catalogue; it never mutates store data.</summary>
public interface ITorobCatalogService
{
    Task<TorobProductsResponse> GetProductsAsync(TorobProductsRequest request, CancellationToken cancellationToken = default);
}
