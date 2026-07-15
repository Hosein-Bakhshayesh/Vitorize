using FluentValidation;
using Vitorize.Application.DTOs.Admin.Products;

namespace Vitorize.Application.Validators.Admin;

public sealed class SaveProductTagRequestValidator : AbstractValidator<SaveProductTagRequestDto>
{
    public SaveProductTagRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(150)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("اسلاگ باید با حروف انگلیسی کوچک، عدد و خط تیره ثبت شود.");
        RuleFor(x => x.Aliases).MaximumLength(1000)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                           value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length <= 20)
            .WithMessage("حداکثر ۲۰ نام مستعار مجاز است.");
    }
}
