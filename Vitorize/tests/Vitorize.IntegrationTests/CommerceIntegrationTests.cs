using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CommerceIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public CommerceIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Cart_identity_sensitive_storage_and_checkout_repricing_work_end_to_end()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: true, price: 100m);
        using var client = _fixture.CreateClient(token);

        var first = await AddAsync(client, product.Id, "REF-ONE");
        first.Items.Should().ContainSingle();
        first.Items[0].Quantity.Should().Be(1);
        first.Items[0].InputValues.Should().ContainSingle(x => x.FieldKey == "customer_reference" && x.IsMasked);

        var merged = await AddAsync(client, product.Id, "REF-ONE");
        merged.Items.Should().ContainSingle();
        merged.Items[0].Quantity.Should().Be(2);

        var separate = await AddAsync(client, product.Id, "REF-TWO");
        separate.Items.Should().HaveCount(2, "different custom inputs are part of cart identity");

        await using (var db = _fixture.CreateDbContext())
        {
            var values = await db.CartItemInputValues
                .Where(x => x.CartItem.Cart.UserId == user.Id)
                .ToListAsync();
            values.Should().OnlyContain(x => x.Value == null && x.EncryptedValue != null);
            values.Should().NotContain(x => x.EncryptedValue == "REF-ONE" || x.EncryptedValue == "REF-TWO");

            var storedProduct = await db.Products.SingleAsync(x => x.Id == product.Id);
            storedProduct.BasePrice = 175m;
            storedProduct.DiscountPrice = 0m;
            await db.SaveChangesAsync();
        }

        var idempotencyKey = $"checkout-{Guid.NewGuid():N}";
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var request = new CheckoutRequestDto();
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", request);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, checkoutBody);
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
        checkout.SubtotalAmount.Should().Be(525m, "three units must be repriced from the current product price");
        checkout.FinalAmount.Should().Be(525m);

        var replay = await client.PostAsJsonAsync("/api/checkout", request);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a completed idempotency key must prevent a second order");

        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.Include(x => x.OrderItems).ThenInclude(x => x.InputValues)
            .SingleAsync(x => x.Id == checkout.OrderId);
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
        (await verify.IdempotencyKeys.SingleAsync(x => x.Key == idempotencyKey)).Status
            .Should().Be((byte)IdempotencyStatus.Completed);
        order.OrderItems.Should().HaveCount(2);
        order.OrderItems.Sum(x => x.Quantity).Should().Be(3);
        order.OrderItems.Should().OnlyContain(x => x.UnitPrice == 175m);
        order.OrderItems.SelectMany(x => x.InputValues)
            .Should().OnlyContain(x => x.Value == null && x.EncryptedValue != null && x.IsSensitive);
        (await verify.Carts.Include(x => x.CartItems).SingleAsync(x => x.UserId == user.Id))
            .CartItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_required_dynamic_input_is_rejected_without_creating_a_cart_item()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: true, withSensitiveRequiredField: true);
        using var client = _fixture.CreateClient(token);

        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = product.Id,
            Quantity = 1
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = _fixture.CreateDbContext();
        (await db.CartItems.CountAsync(x => x.Cart.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Inactive_product_is_hidden_and_cannot_be_added_to_cart()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(active: false, withSensitiveRequiredField: false);
        using var client = _fixture.CreateClient(token);

        (await client.GetAsync($"/api/products/{product.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = product.Id,
            Quantity = 1
        });
        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<CartDto> AddAsync(HttpClient client, Guid productId, string reference)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = productId,
            Quantity = 1,
            InputValues = new Dictionary<string, string?> { ["customer_reference"] = reference }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
    }

    private async Task<Product> CreateProductAsync(bool active, bool withSensitiveRequiredField, decimal price = 100m)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Integration category", Slug = $"integration-{Guid.NewGuid():N}",
            SortOrder = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Integration product",
            Slug = $"product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = price,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = active, CreatedAt = DateTime.UtcNow
        };
        if (withSensitiveRequiredField)
            product.ProductInputFields.Add(new ProductInputField
            {
                Id = Guid.NewGuid(), Key = "customer_reference", Label = "شناسه مشتری",
                FieldType = (byte)ProductInputFieldType.Text, IsRequired = true,
                MinLength = 3, MaxLength = 50, IsSensitive = true,
                DisplayStage = (byte)ProductInputStage.ProductPage, IsActive = true,
                SortOrder = 0, CreatedAt = DateTime.UtcNow
            });
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
