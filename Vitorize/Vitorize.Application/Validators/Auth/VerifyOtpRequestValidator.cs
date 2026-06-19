using FluentValidation;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Validators.Auth
{
    public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequestDto>
    {
        public VerifyOtpRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("کد تایید الزامی است.")
                .Length(6).WithMessage("کد تایید باید 6 رقم باشد.")
                .Matches(@"^\d{6}$").WithMessage("کد تایید معتبر نیست.");

            RuleFor(x => x.Purpose)
                .Must(x => Enum.IsDefined(typeof(OtpPurpose), x))
                .WithMessage("نوع کد تایید معتبر نیست.");
        }
    }
}