using FluentValidation;
using Vitorize.Application.DTOs.Coupons;

namespace Vitorize.Application.Validators.Coupons
{
    public class ValidateCouponRequestValidator
        : AbstractValidator<ValidateCouponRequestDto>
    {
        public ValidateCouponRequestValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty()
                .WithMessage("کد تخفیف الزامی است.")
                .MaximumLength(100);

            RuleFor(x => x.OrderAmount)
                .GreaterThan(0)
                .WithMessage("مبلغ سفارش باید بیشتر از صفر باشد.");
        }
    }
}