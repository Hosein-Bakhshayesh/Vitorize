using FluentValidation;
using Vitorize.Application.DTOs.Tickets;

namespace Vitorize.Application.Validators.Tickets
{
    public class CreateTicketRequestValidator
        : AbstractValidator<CreateTicketRequestDto>
    {
        public CreateTicketRequestValidator()
        {
            RuleFor(x => x.Subject)
                .NotEmpty()
                .WithMessage("عنوان تیکت الزامی است.")
                .MaximumLength(250);

            RuleFor(x => x.Message)
                .NotEmpty()
                .WithMessage("متن پیام الزامی است.")
                .MaximumLength(5000);

            RuleFor(x => x.Department)
                .GreaterThan((byte)0);

            RuleFor(x => x.Priority)
                .GreaterThan((byte)0);

            RuleFor(x => x.AttachmentPath)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.AttachmentPath));
        }
    }
}