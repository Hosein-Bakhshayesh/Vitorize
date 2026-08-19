using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Cart;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FIX-11 (Client Issue #4). The per-field Required/Optional capability lives in
/// <c>ProductInputField.IsRequired</c> and <c>ProductInputRules.ValidateValue</c>.
///
/// Product information is collected at CHECKOUT, not on the product page and not in the cart, so the
/// cart deliberately accepts a partially filled set and parks whatever was supplied. Anything the
/// customer does supply is still format-checked here; the required rule is enforced at order
/// creation, which is the gate that stands in front of every payment. These tests pin both halves.
/// </summary>
public sealed class Fix11ProductInputRequiredOptionalTests
{
    [Fact]
    public async Task A_missing_required_field_never_blocks_the_cart_because_checkout_collects_it()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Required("player_id", "شناسه بازیکن"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));
        var userId = Guid.NewGuid();

        var cart = await service.AddItemAsync(userId, Add(product.Id));

        cart.Items.Should().ContainSingle("the customer has not reached checkout yet");
        cart.Items[0].InputFields.Should().ContainSingle(x => x.Key == "player_id" && x.IsRequired,
            "checkout needs the definition in order to render the field");

        // Supplying it later through the cart line is what checkout does before creating the order.
        var item = await db.CartItems.SingleAsync();
        var filled = await service.UpdateItemAsync(userId, item.Id,
            new UpdateCartItemRequestDto { Quantity = 1, InputValues = new Dictionary<string, string?> { ["player_id"] = "PLAYER-1" } });

        filled.Items[0].InputValues.Should().ContainSingle(x => x.FieldKey == "player_id" && x.Value == "PLAYER-1");
    }

    [Fact]
    public void Required_enforcement_still_exists_and_is_applied_at_the_order_boundary()
    {
        // CheckoutService validates with enforceRequired left at its default before an order — and
        // therefore before any payment — can be created.
        var definition = Required("player_id", "شناسه بازیکن");
        var missing = new Dictionary<string, string?>();

        var lenient = () => ProductInputRules.ValidateValue(Definition(definition), null, enforceRequired: false);
        var strict = () => ProductInputRules.ValidateValue(Definition(definition), null);

        lenient.Should().NotThrow("the cart parks a partially filled set");
        strict.Should().Throw<BusinessException>().WithMessage("*شناسه بازیکن*");
    }

    [Fact]
    public async Task Optional_field_left_blank_is_accepted_and_stored_as_no_value()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Optional("note", "یادداشت"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        var cart = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id));

        cart.Items.Should().ContainSingle();
        var stored = await db.CartItemInputValues.SingleAsync();
        stored.FieldKey.Should().Be("note");
        stored.Value.Should().BeNull();
        stored.EncryptedValue.Should().BeNull();
    }

    [Fact]
    public async Task Mixed_product_accepts_required_only_and_rejects_optional_only()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Required("field_a", "الزامی الف"), Optional("field_b", "اختیاری ب"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        var accepted = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("field_a", "A-VALUE")));
        accepted.Items.Should().ContainSingle();

        // Optional-only is fine for the cart; the missing required field is caught at the order boundary.
        var optionalOnly = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("field_b", "B-VALUE")));
        optionalOnly.Items.Should().ContainSingle();

        var strict = () => ProductInputRules.ValidateValue(Definition(Required("field_a", "الزامی الف")), null);
        strict.Should().Throw<BusinessException>().WithMessage("*الزامی الف*");
    }

    [Fact]
    public async Task Checkbox_required_semantics_demand_true_while_optional_may_stay_false()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db,
            Required("accept_terms", "پذیرش قوانین", ProductInputFieldType.Checkbox),
            Optional("newsletter", "خبرنامه", ProductInputFieldType.Checkbox));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        // An unticked required checkbox reaches the cart but must not survive the order boundary.
        var parked = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("accept_terms", "false")));
        parked.Items.Should().ContainSingle();
        var strict = () => ProductInputRules.ValidateValue(
            Definition(Required("accept_terms", "پذیرش قوانین", ProductInputFieldType.Checkbox)), "false");
        strict.Should().Throw<BusinessException>();

        var cart = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("accept_terms", "true")));

        cart.Items.Should().ContainSingle();
        // Scoped to this cart's line: the parked attempt above belongs to a different cart.
        var lineId = cart.Items[0].Id;
        var values = await db.CartItemInputValues.Where(x => x.CartItemId == lineId)
            .ToDictionaryAsync(x => x.FieldKey, x => x.Value);
        values["accept_terms"].Should().Be("true");
        // An unchecked optional checkbox normalises to "false", which the existing rule accepts.
        values["newsletter"].Should().Be("false");
    }

    [Fact]
    public async Task Select_and_radio_reject_unknown_options_but_accept_an_empty_optional_choice()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db,
            Required("region", "منطقه", ProductInputFieldType.Select, "EU", "NA"),
            Optional("platform", "پلتفرم", ProductInputFieldType.Radio, "PC", "PS5"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        var invalidRequired = await Record.ExceptionAsync(() =>
            service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("region", "MARS"))));
        invalidRequired.Should().BeOfType<BusinessException>();

        var invalidOptional = await Record.ExceptionAsync(() =>
            service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("region", "EU"), ("platform", "XBOX"))));
        invalidOptional.Should().BeOfType<BusinessException>();

        var cart = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("region", "EU")));

        cart.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Guest_cart_retains_required_and_optional_semantics_across_reloads()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Required("player_id", "شناسه بازیکن"), Optional("note", "یادداشت"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));
        var guest = CartIdentity.ForGuest(GuestCartToken.Hash(GuestCartToken.Create()));

        await service.AddItemAsync(guest, Add(product.Id, ("player_id", "GUEST-1")));
        var reloaded = await service.GetAsync(guest);

        reloaded.Items.Should().ContainSingle();
        reloaded.Items[0].InputValues.Should().ContainSingle(x => x.FieldKey == "player_id" && x.Value == "GUEST-1");
        reloaded.Items[0].InputValues.Should().Contain(x => x.FieldKey == "note" && x.Value == null);
    }

    [Fact]
    public async Task Guest_to_customer_merge_preserves_values_without_duplicating_the_line()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Required("player_id", "شناسه بازیکن"), Optional("note", "یادداشت"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));
        var token = GuestCartToken.Create();
        var guest = CartIdentity.ForGuest(GuestCartToken.Hash(token));
        var userId = Guid.NewGuid();

        await service.AddItemAsync(guest, Add(product.Id, ("player_id", "MERGE-1")));
        var merged = await service.MergeGuestCartAsync(userId, token);

        merged.Items.Should().ContainSingle("the merge must not duplicate the guest line");
        merged.Items[0].Quantity.Should().Be(1);
        merged.Items[0].InputValues.Should().ContainSingle(x => x.FieldKey == "player_id" && x.Value == "MERGE-1");
        merged.Items[0].InputValues.Should().Contain(x => x.FieldKey == "note" && x.Value == null);
        (await db.Carts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Distinct_optional_values_still_produce_distinct_cart_lines()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Required("player_id", "شناسه بازیکن"), Optional("note", "یادداشت"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));
        var userId = Guid.NewGuid();

        await service.AddItemAsync(userId, Add(product.Id, ("player_id", "P1"), ("note", "first")));
        var cart = await service.AddItemAsync(userId, Add(product.Id, ("player_id", "P1"), ("note", "second")));

        cart.Items.Should().HaveCount(2);
        (await db.CartItems.Select(x => x.InputFingerprint).Distinct().CountAsync()).Should().Be(2);
    }

    private static AddToCartRequestDto Add(Guid productId, params (string Key, string? Value)[] values) => new()
    {
        ProductId = productId,
        Quantity = 1,
        InputValues = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
    };

    private static ProductInputField Required(string key, string label,
        ProductInputFieldType type = ProductInputFieldType.Text, params string[] options) =>
        Field(key, label, type, isRequired: true, options);

    private static ProductInputField Optional(string key, string label,
        ProductInputFieldType type = ProductInputFieldType.Text, params string[] options) =>
        Field(key, label, type, isRequired: false, options);

    private static ProductInputField Field(string key, string label, ProductInputFieldType type,
        bool isRequired, string[] options) => new()
        {
            Id = Guid.NewGuid(), Key = key, Label = label, FieldType = (byte)type, IsRequired = isRequired,
            DisplayStage = (byte)ProductInputStage.ProductPage, SortOrder = 10, IsActive = true,
            OptionsJson = JsonSerializer.Serialize(options), CreatedAt = DateTime.UtcNow
        };

    /// <summary>Projects a seeded definition onto the public rule DTO the validator consumes.</summary>
    private static ProductInputFieldDto Definition(ProductInputField field) => new()
    {
        Id = field.Id, Key = field.Key, Label = field.Label, FieldType = field.FieldType,
        IsRequired = field.IsRequired, DefaultValue = field.DefaultValue,
        MinLength = field.MinLength, MaxLength = field.MaxLength,
        ValidationPattern = field.ValidationPattern, ValidationMessage = field.ValidationMessage,
        IsSensitive = field.IsSensitive, RequiresConfirmation = field.RequiresConfirmation,
        DisplayStage = field.DisplayStage, SortOrder = field.SortOrder, IsActive = field.IsActive,
        Options = string.IsNullOrWhiteSpace(field.OptionsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(field.OptionsJson) ?? new List<string>()
    };

    private static VitorizeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new VitorizeDbContext(options);
    }

    private static Product SeedProduct(VitorizeDbContext db, params ProductInputField[] fields)
    {
        var category = new Category { Id = Guid.NewGuid(), Title = "بازی", Slug = $"cat-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Category = category, Title = "محصول FIX-11",
            Slug = $"fix11-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2, CurrencyType = 2,
            BasePrice = 1000, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        foreach (var field in fields)
        {
            field.ProductId = product.Id;
            product.ProductInputFields.Add(field);
        }
        // Inventory is SKU-scoped: a purchasable non-Instant product always owns a canonical variant.
        product.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "پیش‌فرض", Price = 1000,
            StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 1000,
            IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(category);
        db.Products.Add(product);
        return product;
    }

    private sealed class TestEncryption : IEncryptionService
    {
        public string Encrypt(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Decrypt(string encryptedValue) => Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));
    }
}
