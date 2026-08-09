using FluentAssertions;
using Vitorize.Shared.Enums;
using Vitorize.Web.Models.Store;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontInputValidatorTests
{
    [Fact]
    public void Required_checkout_text_field_blocks_continuation_and_preserves_saved_values()
    {
        var item = Item("Player one", Field("player_id", required: true));
        item.InputValues.Add(Value("player_id", ""));

        var result = StorefrontInputValidator.ValidateCartCheckout([item]);

        result.IsValid.Should().BeFalse();
        result.InvalidItem.Should().BeSameAs(item);
        result.Validation.Errors.Should().ContainKey("player_id");
        item.InputValues.Single().Value.Should().BeEmpty();
    }

    [Fact]
    public void Required_checkout_checkbox_blocks_when_unchecked()
    {
        var item = Item("Rules", Field("accept_rules", required: true, type: ProductInputFieldType.Checkbox));
        item.InputValues.Add(Value("accept_rules", "false"));

        var result = StorefrontInputValidator.ValidateCartCheckout([item]);

        result.IsValid.Should().BeFalse();
        result.Validation.Errors.Should().ContainKey("accept_rules");
    }

    [Fact]
    public void Optional_checkout_field_can_be_empty()
    {
        var item = Item("Optional", Field("note", required: false));

        StorefrontInputValidator.ValidateCartCheckout([item]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valid_checkout_field_allows_continuation()
    {
        var item = Item("Valid", Field("player_id", required: true, minLength: 3));
        item.InputValues.Add(Value("player_id", "ABC-100"));

        StorefrontInputValidator.ValidateCartCheckout([item]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void First_invalid_cart_item_is_deterministically_identified()
    {
        var valid = Item("Valid", Field("id", required: true)); valid.InputValues.Add(Value("id", "A"));
        var invalid = Item("Invalid", Field("id", required: true)); invalid.InputValues.Add(Value("id", ""));
        var later = Item("Later", Field("id", required: true)); later.InputValues.Add(Value("id", "C"));

        var result = StorefrontInputValidator.ValidateCartCheckout([valid, invalid, later]);

        result.IsValid.Should().BeFalse();
        result.InvalidItem.Should().BeSameAs(invalid);
    }

    [Fact]
    public void First_invalid_field_is_determined_by_sort_order()
    {
        var later = Field("later", required: true, sortOrder: 2);
        var first = Field("first", required: true, sortOrder: 1);

        var result = StorefrontInputValidator.ValidateFields([later, first], new Dictionary<string, string?>());

        result.Errors.Keys.First().Should().Be("first");
    }

    [Fact]
    public void Product_stage_validation_produces_keyed_error_without_mutating_values()
    {
        var values = new Dictionary<string, string?> { ["account_email"] = "invalid" };
        var result = StorefrontInputValidator.ValidateFields(
            [Field("account_email", required: true, type: ProductInputFieldType.Email)], values);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("account_email");
        values["account_email"].Should().Be("invalid");
    }

    [Fact]
    public void Confirmation_error_clears_once_values_match()
    {
        var field = Field("secret", required: true);
        field.RequiresConfirmation = true;
        var values = new Dictionary<string, string?> { ["secret"] = "same" };

        StorefrontInputValidator.ValidateFields([field], values, new Dictionary<string, string?> { ["secret"] = "different" }).IsValid.Should().BeFalse();
        StorefrontInputValidator.ValidateFields([field], values, new Dictionary<string, string?> { ["secret"] = "same" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Product_page_stage_fields_do_not_block_cart_checkout()
    {
        var item = Item("Stage one", Field("product_only", required: true, stage: ProductInputStage.ProductPage));

        StorefrontInputValidator.ValidateCartCheckout([item]).IsValid.Should().BeTrue();
    }

    private static CartItemModel Item(string title, params StoreProductInputFieldModel[] fields) => new()
    {
        Id = Guid.NewGuid(), ProductTitle = title, Quantity = 1, InputFields = fields.ToList()
    };

    private static StoreProductInputValueModel Value(string key, string? value) => new() { FieldKey = key, Value = value };

    private static StoreProductInputFieldModel Field(string key, bool required, ProductInputFieldType type = ProductInputFieldType.Text,
        int sortOrder = 0, int? minLength = null, ProductInputStage stage = ProductInputStage.Checkout) => new()
    {
        Id = Guid.NewGuid(), Key = key, Label = key, FieldType = (byte)type, IsRequired = required,
        MinLength = minLength, DisplayStage = (byte)stage, IsActive = true, SortOrder = sortOrder
    };
}
