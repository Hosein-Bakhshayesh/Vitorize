using FluentValidation;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Validators.Auth
{
    public class SendOtpRequestValidator : AbstractValidator<SendOtpRequestDto>
    {
        public SendOtpRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Purpose)
                .Must(x => Enum.IsDefined(typeof(OtpPurpose), x))
                .WithMessage("نوع کد تایید معتبر نیست.");
        }
    }
}