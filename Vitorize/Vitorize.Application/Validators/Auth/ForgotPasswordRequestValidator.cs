using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequestDto>
    {
        public ForgotPasswordRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل معتبر نیست.");
        }
    }
}