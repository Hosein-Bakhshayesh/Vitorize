using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Vitorize.Infrastructure.Services;
using Vitorize.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Product-required information moved out of the product page and the cart and is now collected at
/// Checkout. These tests pin the contract that matters for money: the cart accepts a partially
/// filled set, and an order — the thing that stands in front of every payment — cannot be created
/// until every required value for every line is present and valid. They also pin that the captured
/// values land on the right line and survive as an immutable order snapshot.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CheckoutProductInformationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public CheckoutProductInformationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_line_with_a_missing_required_value_reaches_the_cart_but_never_becomes_an_order()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(("player_id", "شناسه بازیکن", true), ("note", "یادداشت", false));

        await AddToCartAsync(user.Id, product.Id);

        await using (var verify = _fixture.CreateDbContext())
            (await verify.CartItems.CountAsync(x => x.Cart.UserId == user.Id)).Should().Be(1,
                "the cart never blocks on information collected later");

        var act = () => CheckoutAsync(user.Id);
        (await act.Should().ThrowAsync<BusinessException>()).Which.Message.Should().Contain("شناسه بازیکن");

        await using var final = _fixture.CreateDbContext();
        (await final.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0,
            "no order means no payment could ever have been started");
    }

    [Fact]
    public async Task Filling_the_required_value_at_checkout_creates_the_order_and_snapshots_it()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(("player_id", "شناسه بازیکن", true), ("note", "یادداشت", false));

        var cart = await AddToCartAsync(user.Id, product.Id);
        var lineId = cart.Items.Single().Id;

        // This is exactly what the checkout page does before it allows payment.
        await UpdateLineAsync(user.Id, lineId, new() { ["player_id"] = "PLAYER-77" });
        var result = await CheckoutAsync(user.Id);

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.Include(x => x.InputValues)
            .SingleAsync(x => x.OrderId == result.OrderId);
        item.InputValues.Should().Contain(x => x.FieldKey == "player_id" && x.Value == "PLAYER-77");
        item.InputValues.Should().Contain(x => x.FieldKey == "note" && x.Value == null,
            "an untouched optional field is still snapshotted, with no value");
    }

    [Fact]
    public async Task Each_cart_line_keeps_its_own_values_and_they_never_cross_over()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var first = await SeedProductAsync(("account_email", "ایمیل اکانت", true));
        var second = await SeedProductAsync(("player_id", "شناسه بازیکن", true), ("note", "یادداشت", false));

        var cart = await AddToCartAsync(user.Id, first.Id);
        cart = await AddToCartAsync(user.Id, second.Id);

        var firstLine = cart.Items.Single(x => x.ProductId == first.Id).Id;
        var secondLine = cart.Items.Single(x => x.ProductId == second.Id).Id;

        await UpdateLineAsync(user.Id, firstLine, new() { ["account_email"] = "buyer@example.test" });

        // The second line is still incomplete, so the whole order is refused — not just silently
        // dropped — and the first line's value is not lost by that refusal.
        var act = () => CheckoutAsync(user.Id);
        (await act.Should().ThrowAsync<BusinessException>()).Which.Message.Should().Contain("شناسه بازیکن");
        await using (var mid = _fixture.CreateDbContext())
            (await mid.CartItemInputValues.SingleAsync(x => x.CartItemId == firstLine && x.FieldKey == "account_email"))
                .Value.Should().Be("buyer@example.test", "an unrelated failure must not make the customer retype");

        await UpdateLineAsync(user.Id, secondLine, new() { ["player_id"] = "PLAYER-9" });
        var result = await CheckoutAsync(user.Id);

        await using var verify = _fixture.CreateDbContext();
        var items = await verify.OrderItems.Include(x => x.InputValues)
            .Where(x => x.OrderId == result.OrderId).ToListAsync();
        items.Single(x => x.ProductId == first.Id).InputValues
            .Should().ContainSingle(x => x.FieldKey == "account_email" && x.Value == "buyer@example.test");
        var secondValues = items.Single(x => x.ProductId == second.Id).InputValues;
        secondValues.Should().Contain(x => x.FieldKey == "player_id" && x.Value == "PLAYER-9");
        secondValues.Should().NotContain(x => x.FieldKey == "account_email");
    }

    [Fact]
    public async Task The_order_snapshot_is_immutable_when_the_product_definition_changes_afterwards()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(("player_id", "شناسه بازیکن", true));

        var cart = await AddToCartAsync(user.Id, product.Id);
        await UpdateLineAsync(user.Id, cart.Items.Single().Id, new() { ["player_id"] = "ORIGINAL" });
        var result = await CheckoutAsync(user.Id);

        // An administrator renames the field and adds a new required one after the sale.
        await using (var edit = _fixture.CreateDbContext())
        {
            var field = await edit.ProductInputFields.SingleAsync(x => x.ProductId == product.Id);
            field.Label = "شناسه جدید";
            edit.ProductInputFields.Add(new ProductInputField
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Key = "added_later", Label = "بعداً اضافه شد",
                FieldType = (byte)ProductInputFieldType.Text, IsRequired = true, DisplayStage = 1,
                IsActive = true, SortOrder = 5, CreatedAt = DateTime.UtcNow
            });
            await edit.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateDbContext();
        var snapshot = await verify.OrderItems.Include(x => x.InputValues)
            .SingleAsync(x => x.OrderId == result.OrderId);
        snapshot.InputValues.Should().ContainSingle();
        snapshot.InputValues.Single().FieldLabel.Should().Be("شناسه بازیکن", "the order keeps what was agreed");
        snapshot.InputValues.Single().Value.Should().Be("ORIGINAL");
    }

    [Fact]
    public async Task A_product_without_input_fields_checks_out_with_no_information_step()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync();

        var cart = await AddToCartAsync(user.Id, product.Id);
        cart.Items.Single().InputFields.Should().BeEmpty("checkout renders no card for this line");

        var result = await CheckoutAsync(user.Id);

        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItems.Include(x => x.InputValues).SingleAsync(x => x.OrderId == result.OrderId))
            .InputValues.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- helpers

    private CartService Cart(Vitorize.Infrastructure.Persistence.VitorizeDbContext db) =>
        new(db, _fixture.Factory.Services.GetRequiredService<IEncryptionService>(), new VatSettingsProvider(db));

    private async Task<CartDto> AddToCartAsync(Guid userId, Guid productId)
    {
        await using var db = _fixture.CreateDbContext();
        return await Cart(db).AddItemAsync(userId, new AddToCartRequestDto { ProductId = productId, Quantity = 1 });
    }

    private async Task<CartDto> UpdateLineAsync(Guid userId, Guid lineId, Dictionary<string, string?> values)
    {
        await using var db = _fixture.CreateDbContext();
        return await Cart(db).UpdateItemAsync(userId, lineId,
            new UpdateCartItemRequestDto { Quantity = 1, InputValues = values });
    }

    private async Task<CheckoutResultDto> CheckoutAsync(Guid userId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        return await checkout.CheckoutAsync(userId, new CheckoutRequestDto());
    }

    private async Task<Product> SeedProductAsync(params (string Key, string Label, bool Required)[] fields)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "checkout-inputs", Slug = $"checkout-inputs-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Checkout input product",
            Slug = $"checkout-input-{Guid.NewGuid():N}", ProductType = 1,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 1000m,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var order = 0;
        foreach (var (key, label, required) in fields)
        {
            product.ProductInputFields.Add(new ProductInputField
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Key = key, Label = label,
                FieldType = (byte)(key.Contains("email") ? ProductInputFieldType.Email : ProductInputFieldType.Text),
                IsRequired = required, DisplayStage = 1, IsActive = true, SortOrder = order++,
                CreatedAt = DateTime.UtcNow
            });
        }
        product.WithCanonicalVariant();
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
