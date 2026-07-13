using FluentValidation;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class RequestOtpLoginRequestValidator : AbstractValidator<RequestOtpLoginRequestDto>
    {
        public RequestOtpLoginRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Must(IranMobile.IsValid).WithMessage("شماره موبایل معتبر نیست.");
        }
    }
}
