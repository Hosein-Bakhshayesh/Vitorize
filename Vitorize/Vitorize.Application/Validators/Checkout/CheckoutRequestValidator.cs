using FluentValidation;
using Vitorize.Application.DTOs.Checkout;

namespace Vitorize.Application.Validators.Checkout
{
    public class CheckoutRequestValidator
        : AbstractValidator<CheckoutRequestDto>
    {
        public CheckoutRequestValidator()
        {
            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage("توضیحات نمی‌تواند بیشتر از 1000 کاراکتر باشد.");

            RuleFor(x => x.CouponCode)
                .MaximumLength(100)
                .WithMessage("کد تخفیف معتبر نیست.")
                .When(x => !string.IsNullOrWhiteSpace(x.CouponCode));
        }
    }
}