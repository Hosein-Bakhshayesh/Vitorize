namespace Vitorize.Application.DTOs.Admin.Payments;

public sealed class PaymentDetailHistoryFilterDto
{
    public int Page { get; set; } = 1;
    public int? PageNumber { get; set; }
    public int PageSize { get; set; } = 25;
    public string? SortDirection { get; set; }
}
