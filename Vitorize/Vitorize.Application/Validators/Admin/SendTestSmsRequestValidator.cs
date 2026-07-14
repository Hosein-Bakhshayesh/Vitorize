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

            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.TemplateKey) || !string.IsNullOrWhiteSpace(x.Text))
                .WithMessage("قالب یا متن پیامک را مشخص کنید.");

            When(x => !string.IsNullOrWhiteSpace(x.TemplateKey), () =>
            {
                RuleFor(x => x.TemplateKey!)
                    .Must(x => SmsTemplateContract.GetRequiredParameterNames(x) is not null)
                    .WithMessage("کلید قالب پیامک معتبر نیست.");

                RuleFor(x => x)
                    .Must(HasValidTemplateParameters)
                    .WithMessage("پارامترهای قالب باید دقیقاً CODE/EXPIRE یا ORDER_NUMBER و دارای مقدار باشند.");
            });
        }

        private static bool HasValidTemplateParameters(SendTestSmsRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.TemplateKey))
                return true;

            var parameters = (request.Parameters ?? new List<TestSmsParameterDto>())
                .Select(x => new SmsTemplateParameter(x.Name, x.Value))
                .ToList();

            return SmsTemplateContract.HasExactParameters(request.TemplateKey, parameters);
        }
    }
}
