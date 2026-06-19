using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
    {
        public ChangePasswordRequestValidator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("رمز عبور فعلی الزامی است.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("رمز عبور جدید الزامی است.")
                .MinimumLength(8).WithMessage("رمز عبور جدید باید حداقل 8 کاراکتر باشد.")
                .MaximumLength(100).WithMessage("رمز عبور جدید نمی‌تواند بیشتر از 100 کاراکتر باشد.");

            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("رمز عبور جدید و تکرار آن یکسان نیستند.");
        }
    }
}