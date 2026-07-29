namespace Vitorize.Application.DTOs.Wallet;

public sealed class WalletTransactionFilterDto
{
    public int Page { get; set; } = 1;
    public int? PageNumber { get; set; }
    public int PageSize { get; set; } = 20;
    public byte? Type { get; set; }
    public string? SortDirection { get; set; }
}
