using FluentValidation;
using Vitorize.Application.DTOs.Wallet;

namespace Vitorize.Application.Validators.Wallet
{
    public class WalletChargeRequestValidator
        : AbstractValidator<WalletChargeRequestDto>
    {
        public WalletChargeRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("کاربر معتبر نیست.");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("مبلغ شارژ باید بیشتر از صفر باشد.");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrWhiteSpace(x.Description));
        }
    }
}