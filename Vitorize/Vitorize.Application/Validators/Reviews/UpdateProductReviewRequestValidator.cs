using FluentValidation;
using Vitorize.Application.DTOs.Reviews;

namespace Vitorize.Application.Validators.Reviews
{
    public class UpdateProductReviewRequestValidator
        : AbstractValidator<UpdateProductReviewRequestDto>
    {
        public UpdateProductReviewRequestValidator()
        {
            RuleFor(x => x.Comment)
                .NotEmpty()
                .WithMessage("متن نظر الزامی است.")
                .MaximumLength(2000)
                .WithMessage("متن نظر نمی‌تواند بیش از ۲۰۰۰ کاراکتر باشد.");

            RuleFor(x => x.Title)
                .MaximumLength(200)
                .WithMessage("عنوان نظر نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.")
                .When(x => !string.IsNullOrWhiteSpace(x.Title));

            RuleFor(x => x.Rating)
                .InclusiveBetween((byte)1, (byte)5)
                .WithMessage("امتیاز باید بین ۱ تا ۵ باشد.");
        }
    }
}
