using FluentValidation;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Validators.Payments;

public sealed class PaymentRefundRequestValidator : AbstractValidator<PaymentRefundRequestDto>
{
    public PaymentRefundRequestValidator()
    {
        RuleFor(x => x.Method).Must(x => Enum.IsDefined(typeof(PaymentRefundMethod), x));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100)
            .Matches("^[A-Za-z0-9._:-]+$");
    }
}
