using FluentValidation;
using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Validators.Auth
{
    public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequestDto>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh Token الزامی است.");
        }
    }
}