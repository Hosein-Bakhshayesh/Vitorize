using FluentValidation;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Sms;

namespace Vitorize.Application.Validators.Admin
{
    public sealed class SendCustomNotificationRequestValidator : AbstractValidator<SendCustomNotificationRequestDto>
    {
        public SendCustomNotificationRequestValidator()
        {
            RuleFor(x => x)
                .Must(x => x.UserId.HasValue || IranMobile.TryNormalize(x.Mobile, out _))
                .WithMessage("کاربر یا شماره موبایل معتبر الزامی است.");
            RuleFor(x => x.OrderNumber)
                .NotEmpty().WithMessage("کد پیگیری الزامی است.")
                .MaximumLength(150).WithMessage("کد پیگیری حداکثر ۱۵۰ نویسه است.")
                .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
                .WithMessage("کد پیگیری فقط می‌تواند شامل حروف لاتین، عدد، خط تیره، نقطه و زیرخط باشد.");
            RuleFor(x => x.InternalNote).MaximumLength(500);
            RuleFor(x => x.IdempotencyKey).MaximumLength(100);
        }
    }

    public sealed class SendCustomTextRequestValidator : AbstractValidator<SendCustomTextRequestDto>
    {
        public SendCustomTextRequestValidator()
        {
            RuleFor(x => x)
                .Must(x => x.UserId.HasValue || IranMobile.TryNormalize(x.Mobile, out _))
                .WithMessage("کاربر یا شماره موبایل معتبر الزامی است.");
            RuleFor(x => x.Text)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("متن پیامک الزامی است.")
                .MaximumLength(2000).WithMessage("متن پیامک بیش از حد طولانی است.")
                .Must(x => !x.Contains('<') && !x.Contains('>') && !x.Any(char.IsControl))
                .WithMessage("HTML، اسکریپت و نویسه کنترلی مجاز نیست.");
            RuleFor(x => x.InternalNote).MaximumLength(500);
            RuleFor(x => x.IdempotencyKey).MaximumLength(100);
        }
    }
}
