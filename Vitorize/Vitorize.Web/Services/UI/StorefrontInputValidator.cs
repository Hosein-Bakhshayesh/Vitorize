using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Products;
using Vitorize.Shared.Exceptions;
using Vitorize.Web.Models.Store;

namespace Vitorize.Web.Services.UI;

/// <summary>Applies the API's product-input rules to storefront values before navigation or mutation.</summary>
public static class StorefrontInputValidator
{
    public static StorefrontInputValidationResult ValidateFields(
        IEnumerable<StoreProductInputFieldModel> fields,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyDictionary<string, string?>? confirmations = null,
        IEnumerable<StoreProductInputValueModel>? persistedValues = null)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var persisted = persistedValues?.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, StoreProductInputValueModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
        {
            values.TryGetValue(field.Key, out var value);
            var isMaskedPersistedValue = field.IsSensitive && persisted.TryGetValue(field.Key, out var persistedValue) && persistedValue.IsMasked;
            if (!isMaskedPersistedValue)
            {
                try { ProductInputRules.ValidateValue(ToDefinition(field), value); }
                catch (BusinessException exception) { errors[field.Key] = exception.Message; }
            }

            if (!errors.ContainsKey(field.Key) && field.RequiresConfirmation &&
                !string.Equals(value, confirmations?.GetValueOrDefault(field.Key), StringComparison.Ordinal))
            {
                errors[field.Key] = $"تکرار «{field.Label}» یکسان نیست.";
            }
        }

        return new StorefrontInputValidationResult(errors);
    }

    public static CartCheckoutValidationResult ValidateCartCheckout(IEnumerable<CartItemModel> items)
    {
        foreach (var item in items)
        {
            var values = item.InputValues.ToDictionary(x => x.FieldKey, x => x.Value, StringComparer.OrdinalIgnoreCase);
            var result = ValidateFields(item.InputFields.Where(x => x.DisplayStage == 2), values, persistedValues: item.InputValues);
            if (!result.IsValid) return new CartCheckoutValidationResult(item, result);
        }

        return new CartCheckoutValidationResult(null, StorefrontInputValidationResult.Valid);
    }

    private static ProductInputFieldDto ToDefinition(StoreProductInputFieldModel field) => new()
    {
        Id = field.Id, Key = field.Key, Label = field.Label, Description = field.Description,
        Placeholder = field.Placeholder, FieldType = field.FieldType, IsRequired = field.IsRequired,
        Options = field.Options, DefaultValue = field.DefaultValue, MinLength = field.MinLength,
        MaxLength = field.MaxLength, ValidationPattern = field.ValidationPattern,
        ValidationMessage = field.ValidationMessage, IsSensitive = field.IsSensitive,
        RequiresConfirmation = field.RequiresConfirmation, DisplayStage = field.DisplayStage,
        SortOrder = field.SortOrder, IsActive = field.IsActive
    };
}

public sealed class StorefrontInputValidationResult(IReadOnlyDictionary<string, string> errors)
{
    public static StorefrontInputValidationResult Valid { get; } = new(new Dictionary<string, string>());
    public IReadOnlyDictionary<string, string> Errors { get; } = errors;
    public bool IsValid => Errors.Count == 0;
}

public sealed class CartCheckoutValidationResult(CartItemModel? invalidItem, StorefrontInputValidationResult validation)
{
    public CartItemModel? InvalidItem { get; } = invalidItem;
    public StorefrontInputValidationResult Validation { get; } = validation;
    public bool IsValid => InvalidItem is null && Validation.IsValid;
}
