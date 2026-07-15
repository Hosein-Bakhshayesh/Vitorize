using FluentValidation;
using Vitorize.Application.DTOs.Admin.Orders;

namespace Vitorize.Application.Validators.Admin;

public sealed class ManualDeliveryRequestValidator : AbstractValidator<ManualDeliveryRequestDto>
{
    public ManualDeliveryRequestValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}
