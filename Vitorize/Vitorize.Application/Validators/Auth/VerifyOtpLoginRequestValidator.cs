using FluentValidation;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class VerifyOtpLoginRequestValidator : AbstractValidator<VerifyOtpLoginRequestDto>
    {
        public VerifyOtpLoginRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Must(IranMobile.IsValid).WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("کد تایید الزامی است.")
                .Matches(@"^\d{4,8}$").WithMessage("کد تایید معتبر نیست.");
        }
    }
}
