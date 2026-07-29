namespace Vitorize.Application.DTOs.Tickets;

public sealed class AdminTicketFilterDto
{
    public string? Search { get; set; }
    public byte? Status { get; set; }
    public byte? Department { get; set; }
    public int Page { get; set; } = 1;
    public int? PageNumber { get; set; }
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
