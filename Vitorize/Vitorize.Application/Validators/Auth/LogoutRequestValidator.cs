using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class LogoutRequestValidator : AbstractValidator<LogoutRequestDto>
    {
        public LogoutRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh Token الزامی است.");
        }
    }
}