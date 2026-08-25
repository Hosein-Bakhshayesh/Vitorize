using FluentValidation;
using Vitorize.Application.DTOs.Admin.Users;

namespace Vitorize.Application.Validators.Admin
{
    /// <summary>
    /// Same password rules the account holder would face changing it themselves. An administrator
    /// acting on someone's behalf must not be able to set a weaker password than the owner could.
    /// </summary>
    public class AdminResetPasswordRequestValidator : AbstractValidator<AdminResetPasswordRequestDto>
    {
        public AdminResetPasswordRequestValidator()
        {
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("رمز عبور جدید الزامی است.")
                .MinimumLength(8).WithMessage("رمز عبور جدید باید حداقل 8 کاراکتر باشد.")
                .MaximumLength(100).WithMessage("رمز عبور جدید نمی‌تواند بیشتر از 100 کاراکتر باشد.");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.NewPassword).WithMessage("رمز عبور جدید و تکرار آن یکسان نیستند.");
        }
    }
}
