using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class UpdateProfileRequestValidator
        : AbstractValidator<UpdateProfileRequestDto>
    {
        public UpdateProfileRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty()
                .WithMessage("نام و نام خانوادگی الزامی است.")
                .MaximumLength(200)
                .WithMessage("نام و نام خانوادگی حداکثر ۲۰۰ کاراکتر است.");

            RuleFor(x => x.Email)
                .EmailAddress()
                .WithMessage("ایمیل معتبر نیست.")
                .MaximumLength(320)
                .WithMessage("ایمیل حداکثر ۳۲۰ کاراکتر است.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        }
    }
}
