using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Api.Services;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-11 (Client Issue #4) through the real HTTP surface and SQL Server. Proves the existing
/// <c>ProductInputField.IsRequired</c> definition stays authoritative on the server — browser
/// validation can be bypassed, but cart and checkout both enforce it — and that optional fields
/// are genuinely optional all the way into the immutable order snapshot.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix11ProductInputRequiredOptionalIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix11ProductInputRequiredOptionalIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Server_rejects_a_missing_required_value_even_when_browser_validation_is_bypassed()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync();
        using var client = _fixture.CreateClient(token);

        // Raw API call: no storefront form, no client-side validation.
        var omitted = await client.PostAsJsonAsync("/api/cart/items", Add(product.Id));
        var blank = await client.PostAsJsonAsync("/api/cart/items", Add(product.Id, ("player_id", "   ")));
        var optionalOnly = await client.PostAsJsonAsync("/api/cart/items", Add(product.Id, ("note", "فقط اختیاری")));

        omitted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        optionalOnly.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = _fixture.CreateDbContext();
        (await db.CartItems.CountAsync(x => x.Cart.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Optional_field_left_blank_survives_cart_checkout_and_the_order_snapshot()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync();
        using var client = _fixture.CreateClient(token);

        var added = await client.PostAsJsonAsync("/api/cart/items", Add(product.Id, ("player_id", "PLAYER-11")));
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        var cart = (await added.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        cart.Items.Should().ContainSingle();
        cart.Items[0].InputValues.Should().Contain(x => x.FieldKey == "player_id" && x.Value == "PLAYER-11");
        cart.Items[0].InputValues.Should().Contain(x => x.FieldKey == "note" && x.Value == null);

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix11-{Guid.NewGuid():N}");
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, await checkoutResponse.Content.ReadAsStringAsync());
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;

        await using var db = _fixture.CreateDbContext();
        var snapshot = await db.OrderItemInputValues
            .Where(x => x.OrderItem.OrderId == checkout.OrderId)
            .ToListAsync();

        snapshot.Should().HaveCount(2);
        var required = snapshot.Single(x => x.FieldKey == "player_id");
        required.FieldLabel.Should().Be("شناسه بازیکن");
        required.FieldType.Should().Be((byte)ProductInputFieldType.Text);
        required.Value.Should().Be("PLAYER-11");
        required.IsSensitive.Should().BeFalse();

        var optional = snapshot.Single(x => x.FieldKey == "note");
        optional.FieldLabel.Should().Be("یادداشت");
        optional.Value.Should().BeNull();
        optional.EncryptedValue.Should().BeNull();

        (await db.Orders.SingleAsync(x => x.Id == checkout.OrderId)).UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Checkout_revalidates_required_fields_that_the_cart_stage_never_asked_for()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(withRequiredCheckoutStageField: true);
        using var client = _fixture.CreateClient(token);

        // The add-to-cart stage only validates product-page fields, so this succeeds …
        var added = await client.PostAsJsonAsync("/api/cart/items", Add(product.Id, ("player_id", "PLAYER-12")));
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());

        // … and checkout must still refuse to create an order while the checkout-stage
        // required field is empty. Removing this revalidation would let the order through.
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix11-stage2-{Guid.NewGuid():N}");
        var blocked = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest, await blocked.Content.ReadAsStringAsync());

        await using (var db = _fixture.CreateDbContext())
        {
            (await db.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        }

        var cart = (await (await client.GetAsync("/api/cart")).Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        var supplied = await client.PutAsJsonAsync($"/api/cart/items/{cart.Items[0].Id}", new UpdateCartItemRequestDto
        {
            Quantity = 1,
            InputValues = new Dictionary<string, string?>
            {
                ["player_id"] = "PLAYER-12",
                ["delivery_window"] = "صبح",
                ["note"] = null
            }
        });
        supplied.StatusCode.Should().Be(HttpStatusCode.OK, await supplied.Content.ReadAsStringAsync());

        using var second = _fixture.CreateClient(token);
        second.DefaultRequestHeaders.Add("Idempotency-Key", $"fix11-stage2-ok-{Guid.NewGuid():N}");
        var accepted = await second.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Guest_cart_keeps_required_and_optional_values_through_the_login_merge()
    {
        var product = await CreateProductAsync();
        var guestToken = GuestCartToken.Create();
        using var guestClient = _fixture.CreateClient();
        guestClient.DefaultRequestHeaders.Add(CartIdentityResolver.GuestHeader, guestToken);

        var blocked = await guestClient.PostAsJsonAsync("/api/cart/items", Add(product.Id, ("note", "فقط اختیاری")));
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a guest is held to the same required rule");

        var guestAdd = await guestClient.PostAsJsonAsync("/api/cart/items", Add(product.Id, ("player_id", "GUEST-11")));
        guestAdd.StatusCode.Should().Be(HttpStatusCode.OK, await guestAdd.Content.ReadAsStringAsync());

        // The guest cart must survive a fresh request that carries only the guest capability.
        using var reloadClient = _fixture.CreateClient();
        reloadClient.DefaultRequestHeaders.Add(CartIdentityResolver.GuestHeader, guestToken);
        var reloaded = (await (await reloadClient.GetAsync("/api/cart")).Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        reloaded.Items.Should().ContainSingle();
        reloaded.Items[0].InputValues.Should().Contain(x => x.FieldKey == "player_id" && x.Value == "GUEST-11");
        reloaded.Items[0].InputValues.Should().Contain(x => x.FieldKey == "note" && x.Value == null);

        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var merged = _fixture.CreateClient(token);
        var mergeResponse = await merged.PostAsJsonAsync("/api/cart/merge-guest", new { guestToken });
        mergeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await mergeResponse.Content.ReadAsStringAsync());
        var mergedCart = (await mergeResponse.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;

        mergedCart.Items.Should().ContainSingle("a merge must not duplicate the guest line");
        mergedCart.Items[0].Quantity.Should().Be(1);
        mergedCart.Items[0].InputValues.Should().Contain(x => x.FieldKey == "player_id" && x.Value == "GUEST-11");
        mergedCart.Items[0].InputValues.Should().Contain(x => x.FieldKey == "note" && x.Value == null);

        await using var db = _fixture.CreateDbContext();
        (await db.CartItems.CountAsync(x => x.Cart.UserId == user.Id)).Should().Be(1);
    }

    private static AddToCartRequestDto Add(Guid productId, params (string Key, string? Value)[] values) => new()
    {
        ProductId = productId,
        Quantity = 1,
        InputValues = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
    };

    private async Task<Product> CreateProductAsync(bool withRequiredCheckoutStageField = false)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "FIX-11 category", Slug = $"fix11-{Guid.NewGuid():N}",
            SortOrder = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX-11 product",
            Slug = $"fix11-product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 100m,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.ProductInputFields.Add(new ProductInputField
        {
            Id = Guid.NewGuid(), Key = "player_id", Label = "شناسه بازیکن",
            FieldType = (byte)ProductInputFieldType.Text, IsRequired = true, MinLength = 3, MaxLength = 50,
            DisplayStage = (byte)ProductInputStage.ProductPage, IsActive = true, SortOrder = 10,
            CreatedAt = DateTime.UtcNow
        });
        product.ProductInputFields.Add(new ProductInputField
        {
            Id = Guid.NewGuid(), Key = "note", Label = "یادداشت",
            FieldType = (byte)ProductInputFieldType.Text, IsRequired = false, MaxLength = 200,
            DisplayStage = (byte)ProductInputStage.ProductPage, IsActive = true, SortOrder = 20,
            CreatedAt = DateTime.UtcNow
        });
        if (withRequiredCheckoutStageField)
            product.ProductInputFields.Add(new ProductInputField
            {
                Id = Guid.NewGuid(), Key = "delivery_window", Label = "بازه تحویل",
                FieldType = (byte)ProductInputFieldType.Text, IsRequired = true, MaxLength = 50,
                DisplayStage = (byte)ProductInputStage.Checkout, IsActive = true, SortOrder = 30,
                CreatedAt = DateTime.UtcNow
            });
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
