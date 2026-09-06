using FluentValidation;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Validators.Admin
{
    public sealed class SendTestSmsRequestValidator : AbstractValidator<SendTestSmsRequestDto>
    {
        public SendTestSmsRequestValidator()
        {
            RuleFor(x => x.Mobile)
                .Must(x => IranMobile.TryNormalize(x, out _))
                .WithMessage("شماره موبایل معتبر نیست.");

            RuleFor(x => x.Text)
                .NotEmpty()
                .WithMessage("متن پیامک را مشخص کنید.");

            RuleFor(x => x.TemplateKey)
                .Empty()
                .WithMessage("ارسال قالبی پیامک غیرفعال است.");
        }
    }
}
