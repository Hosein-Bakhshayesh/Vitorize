using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequestDto>
    {
        public ResetPasswordRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("کد تایید الزامی است.")
                .Length(6).WithMessage("کد تایید باید 6 رقم باشد.")
                .Matches(@"^\d{6}$").WithMessage("کد تایید معتبر نیست.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("رمز عبور جدید الزامی است.")
                .MinimumLength(8).WithMessage("رمز عبور جدید باید حداقل 8 کاراکتر باشد.")
                .MaximumLength(100).WithMessage("رمز عبور جدید نمی‌تواند بیشتر از 100 کاراکتر باشد.");

            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("رمز عبور جدید و تکرار آن یکسان نیستند.");
        }
    }
}