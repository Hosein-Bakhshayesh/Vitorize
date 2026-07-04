using FluentValidation;
using Vitorize.Application.DTOs.Wallet;

namespace Vitorize.Application.Validators.Wallet
{
    public class WalletTopUpRequestValidator
        : AbstractValidator<WalletTopUpRequestDto>
    {
        public WalletTopUpRequestValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("مبلغ شارژ باید بیشتر از صفر باشد.")
                .LessThanOrEqualTo(500_000_000)
                .WithMessage("مبلغ شارژ بیش از حد مجاز است.");
        }
    }
}
