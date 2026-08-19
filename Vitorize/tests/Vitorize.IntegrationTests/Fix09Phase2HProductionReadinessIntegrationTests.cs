using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Api.Controllers;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Cross-cutting production-readiness checks that intentionally exercise the
/// independent HTTP/DbContext paths used by real guest cart requests.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2HProductionReadinessIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix09Phase2HProductionReadinessIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Guest_cart_read_racing_update_is_deadlock_free_and_lossless_over_30_deterministic_rounds()
    {
        var product = await SeedProductAsync();
        var guestToken = GuestCartToken.Create();
        using var setupClient = GuestClient(guestToken);
        var added = await setupClient.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = (await added.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        var itemId = cart.Items.Should().ContainSingle().Subject.Id;

        for (var round = 0; round < 30; round++)
        {
            using var reader = GuestClient(guestToken);
            using var writer = GuestClient(guestToken);
            using var barrier = new Barrier(2);
            var quantity = round % 2 + 1;

            var read = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await reader.GetAsync("/api/cart");
            });
            var update = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await writer.PutAsJsonAsync($"/api/cart/items/{itemId}", new UpdateCartItemRequestDto { Quantity = quantity });
            });

            var results = await Task.WhenAll(read, update);
            results.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        }

        var final = await setupClient.GetFromJsonAsync<ApiResult<CartDto>>("/api/cart");
        final!.Data!.Items.Should().ContainSingle().Which.Id.Should().Be(itemId);
        final.Data.TotalQuantity.Should().BeInRange(1, 2);
    }

    [Fact]
    public async Task Concurrent_login_merges_consume_one_guest_capability_without_duplicate_quantity()
    {
        var product = await SeedProductAsync();
        var guestToken = GuestCartToken.Create();
        using var guest = GuestClient(guestToken);
        (await guest.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 })).StatusCode.Should().Be(HttpStatusCode.OK);

        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var first = _fixture.CreateClient(token);
        using var second = _fixture.CreateClient(token);
        using var barrier = new Barrier(2);
        var request = new CartController.MergeGuestCartRequest(guestToken);
        var mergeOne = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await first.PostAsJsonAsync("/api/cart/merge-guest", request);
        });
        var mergeTwo = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await second.PostAsJsonAsync("/api/cart/merge-guest", request);
        });

        var results = await Task.WhenAll(mergeOne, mergeTwo);
        results.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);

        await using var verify = _fixture.CreateDbContext();
        var userCart = await verify.Carts.Include(x => x.CartItems).SingleAsync(x => x.UserId == user.Id);
        userCart.CartItems.Should().ContainSingle().Which.Quantity.Should().Be(2);
        (await verify.Carts.AnyAsync(x => x.GuestTokenHash == GuestCartToken.Hash(guestToken))).Should().BeFalse();
    }

    private HttpClient GuestClient(string guestToken)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", guestToken);
        return client;
    }

    private async Task<Product> SeedProductAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Phase 2H cart category", Slug = $"p2h-cart-category-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Phase 2H cart product", Slug = $"p2h-cart-product-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = 2, BasePrice = 100m, CurrencyType = 2,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.WithCanonicalVariant();
        await using var db = _fixture.CreateDbContext();
        db.AddRange(category, product);
        await db.SaveChangesAsync();
        return product;
    }
}
