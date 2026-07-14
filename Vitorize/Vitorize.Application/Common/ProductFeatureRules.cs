using Vitorize.Application.DTOs.Products;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

public static class ProductFeatureRules
{
    public static void Validate(ProductFeatureDto feature)
    {
        feature.Title = feature.Title?.Trim() ?? string.Empty;
        feature.Value = feature.Value?.Trim() ?? string.Empty;
        feature.IconKey = LucideIconRules.NormalizeOptional(feature.IconKey);
        if (feature.Title.Length is < 1 or > 120 || feature.Value.Length is < 1 or > 500)
            throw new BusinessException("عنوان یا مقدار ویژگی محصول معتبر نیست.");
    }
}
