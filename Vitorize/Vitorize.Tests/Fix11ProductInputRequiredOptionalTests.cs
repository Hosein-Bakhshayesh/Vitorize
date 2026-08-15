using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Cart;
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
/// FIX-11 (Client Issue #4). The per-field Required/Optional capability already exists through
/// <c>ProductInputField.IsRequired</c> and <c>ProductInputRules.ValidateValue</c>; these are the
/// permanent regressions that prove the behaviour end to end across cart, guest cart and merge.
/// </summary>
public sealed class Fix11ProductInputRequiredOptionalTests
{
    [Fact]
    public async Task Required_field_blocks_the_cart_when_missing_and_passes_once_supplied()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, Required("player_id", "شناسه بازیکن"));
        await db.SaveChangesAsync();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));
        var userId = Guid.NewGuid();

        var missing = await Record.ExceptionAsync(() => service.AddItemAsync(userId, Add(product.Id)));
        var empty = await Record.ExceptionAsync(() => service.AddItemAsync(userId, Add(product.Id, ("player_id", "   "))));

        missing.Should().BeOfType<BusinessException>();
        empty.Should().BeOfType<BusinessException>();
        (await db.CartItems.CountAsync()).Should().Be(0);

        var cart = await service.AddItemAsync(userId, Add(product.Id, ("player_id", "PLAYER-1")));

        cart.Items.Should().ContainSingle();
        cart.Items[0].InputValues.Should().ContainSingle(x => x.FieldKey == "player_id" && x.Value == "PLAYER-1");
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

        var rejected = await Record.ExceptionAsync(() =>
            service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("field_b", "B-VALUE"))));

        rejected.Should().BeOfType<BusinessException>()
            .Which.Message.Should().Contain("الزامی الف");
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

        var unchecked_ = await Record.ExceptionAsync(() =>
            service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("accept_terms", "false"))));
        unchecked_.Should().BeOfType<BusinessException>();

        var cart = await service.AddItemAsync(Guid.NewGuid(), Add(product.Id, ("accept_terms", "true")));

        cart.Items.Should().ContainSingle();
        var values = await db.CartItemInputValues.ToDictionaryAsync(x => x.FieldKey, x => x.Value);
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

        var blocked = await Record.ExceptionAsync(() => service.AddItemAsync(guest, Add(product.Id, ("note", "N"))));
        blocked.Should().BeOfType<BusinessException>();

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
