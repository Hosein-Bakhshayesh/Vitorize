namespace Vitorize.Application.DTOs.Admin.Orders;

public sealed class ManualDeliveryRequestDto
{
    public Guid OrderItemId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsVisibleToCustomer { get; set; } = true;
}
