using FluentValidation;
using Vitorize.Application.DTOs.Reviews;

namespace Vitorize.Application.Validators.Reviews
{
    public class ProductReviewVoteRequestValidator
        : AbstractValidator<ProductReviewVoteRequestDto>
    {
        public ProductReviewVoteRequestValidator()
        {
            RuleFor(x => x.VoteType)
                .InclusiveBetween((byte)1, (byte)2)
                .WithMessage("نوع رأی معتبر نیست. (۱ مفید، ۲ غیرمفید)");
        }
    }
}
