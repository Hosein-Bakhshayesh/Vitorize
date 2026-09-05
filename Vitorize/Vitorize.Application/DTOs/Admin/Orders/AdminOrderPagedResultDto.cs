using Vitorize.Application.DTOs.Orders;
using Vitorize.Shared.Common;

namespace Vitorize.Application.DTOs.Admin.Orders;

/// <summary>
/// A page of orders together with status facets for the active non-status filters.
/// </summary>
public sealed class AdminOrderPagedResultDto : PagedResult<OrderDto>
{
    public List<AdminOrderStatusCountDto> StatusCounts { get; set; } = new();

    public int StatusTotalCount { get; set; }
}

public sealed class AdminOrderStatusCountDto
{
    public byte Status { get; set; }

    public int Count { get; set; }
}
