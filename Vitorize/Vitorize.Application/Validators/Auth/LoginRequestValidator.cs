using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("رمز عبور الزامی است.");
        }
    }
}