namespace Vitorize.Application.DTOs.Verification;

public sealed class SetOrderItemKycDeadlineRequestDto
{
    public DateTime NewDeadlineAt { get; set; }
}

public sealed class OrderItemKycDeadlineOperationDto
{
    public Guid OrderItemId { get; set; }
    public byte LifecycleStatus { get; set; }
    public DateTime? CustomerActionDeadlineAt { get; set; }
    public bool Changed { get; set; }
}

public sealed record CustomerDeadlineEnforcementResult(int ExpiredCount, int EligibleCount);
