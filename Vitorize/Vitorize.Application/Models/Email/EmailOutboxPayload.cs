namespace Vitorize.Application.Models.Email;

/// <summary>Durable, plain-text email work item. It contains the final order snapshot so a retry
/// never needs to infer historical order data from rows that may later change.</summary>
public sealed class EmailOutboxPayload
{
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class PaidOrderEmailRequest
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerMobile { get; init; } = string.Empty;
    public string? CustomerEmail { get; init; }
    public decimal FinalAmount { get; init; }
    public List<PaidOrderEmailItem> Items { get; init; } = new();
}

public sealed class PaidOrderEmailItem
{
    public string ProductTitle { get; init; } = string.Empty;
    public string? VariantTitle { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public readonly record struct EmailSendResult(bool IsSuccess, bool Retryable, string? Error)
{
    public static EmailSendResult Sent() => new(true, false, null);
    public static EmailSendResult Skipped() => new(true, false, null);
    public static EmailSendResult Failed(string error, bool retryable) => new(false, retryable, error);
}
