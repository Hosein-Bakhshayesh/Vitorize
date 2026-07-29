namespace Vitorize.Application.DTOs.Tickets;

public sealed class TicketMessageFilterDto
{
    public int Page { get; set; } = 1;
    public int? PageNumber { get; set; }
    public int PageSize { get; set; } = 25;
    public bool? IncludeInternalNotes { get; set; }
    public string? SortDirection { get; set; }
}
