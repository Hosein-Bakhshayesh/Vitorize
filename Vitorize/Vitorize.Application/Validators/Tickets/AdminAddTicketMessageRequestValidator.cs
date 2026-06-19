using FluentValidation;
using Vitorize.Application.DTOs.Tickets;

namespace Vitorize.Application.Validators.Tickets
{
    public class AdminAddTicketMessageRequestValidator
        : AbstractValidator<AdminAddTicketMessageRequestDto>
    {
        public AdminAddTicketMessageRequestValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty()
                .WithMessage("متن پیام الزامی است.")
                .MaximumLength(5000);

            RuleFor(x => x.AttachmentPath)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.AttachmentPath));
        }
    }
}