using FluentValidation;
using Vitorize.Application.DTOs.Wallet;

namespace Vitorize.Application.Validators.Wallet
{
    public class WalletWithdrawRequestValidator
        : AbstractValidator<WalletWithdrawRequestDto>
    {
        public WalletWithdrawRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("کاربر معتبر نیست.");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("مبلغ برداشت باید بیشتر از صفر باشد.");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrWhiteSpace(x.Description));
        }
    }
}