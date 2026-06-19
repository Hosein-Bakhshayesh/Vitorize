using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("نام و نام خانوادگی الزامی است.")
                .MaximumLength(200).WithMessage("نام و نام خانوادگی نمی‌تواند بیشتر از 200 کاراکتر باشد.");

            RuleFor(x => x.Mobile)
                .NotEmpty().WithMessage("شماره موبایل الزامی است.")
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("ایمیل معتبر نیست.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("رمز عبور الزامی است.")
                .MinimumLength(8).WithMessage("رمز عبور باید حداقل 8 کاراکتر باشد.")
                .MaximumLength(100).WithMessage("رمز عبور نمی‌تواند بیشتر از 100 کاراکتر باشد.");
        }
    }
}