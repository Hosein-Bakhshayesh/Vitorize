using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// A cart must only ever change because somebody changed it.
///
/// Reading it, navigating around, losing the identity for one request, or failing to reach the API are
/// none of them reasons for items to disappear. These tests hold that at the boundary where it can
/// actually go wrong - identity resolution and the read path - because that is where "the cart
/// suddenly became empty" would come from.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CartPersistenceIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public CartPersistenceIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- guest identity

    [Fact]
    public async Task Repeated_reads_never_change_a_guest_cart()
    {
        var product = await SeedProductAsync();
        var (token, client) = await GuestWithCartAsync(product.Id, quantity: 2);

        // Fifty reads stand in for fifty page views.
        for (var i = 0; i < 50; i++)
        {
            var cart = await ReadAsync(client);
            cart.Items.Should().ContainSingle();
            cart.TotalQuantity.Should().Be(2);
        }

        await using var db = _fixture.CreateDbContext();
        var stored = await db.Carts.Include(x => x.CartItems)
            .SingleAsync(x => x.GuestTokenHash == GuestCartToken.Hash(token));
        stored.CartItems.Should().ContainSingle();
        stored.CartItems.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task A_request_without_the_guest_identity_is_refused_rather_than_answered_with_an_empty_cart()
    {
        // This is the distinction that matters: if a missing identity produced an empty cart, a single
        // request that forgot the header would look exactly like the customer's cart being cleared.
        var product = await SeedProductAsync();
        var (_, client) = await GuestWithCartAsync(product.Id, quantity: 1);

        using var anonymous = _fixture.CreateClient();
        var response = await anonymous.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadAsync(client)).Items.Should().ContainSingle("the real cart is untouched");
    }

    [Fact]
    public async Task A_malformed_guest_token_cannot_reach_or_replace_another_cart()
    {
        var product = await SeedProductAsync();
        var (token, client) = await GuestWithCartAsync(product.Id, quantity: 1);

        using var bogus = _fixture.CreateClient();
        bogus.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", "not-a-real-token");
        (await bogus.GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var other = _fixture.CreateClient();
        other.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", GuestCartToken.Create());
        var otherCart = await ReadAsync(other);
        otherCart.Items.Should().BeEmpty("a different guest has a different cart");

        (await ReadAsync(client)).Items.Should().ContainSingle();
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_same_guest_token_resolves_to_the_same_cart_from_a_fresh_connection()
    {
        // Equivalent of a browser restart, or the Web process recycling: same cookie, new connection.
        var product = await SeedProductAsync();
        var (token, _) = await GuestWithCartAsync(product.Id, quantity: 3);

        using var reconnected = _fixture.CreateClient();
        reconnected.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", token);

        var cart = await ReadAsync(reconnected);
        cart.Items.Should().ContainSingle();
        cart.TotalQuantity.Should().Be(3);
    }

    // ---------------------------------------------------------------- authenticated identity

    [Fact]
    public async Task An_authenticated_cart_follows_the_user_not_the_connection()
    {
        var product = await SeedProductAsync();
        var (user, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var first = _fixture.CreateClient(accessToken);
        await first.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });

        // A different connection with the same bearer is the same cart.
        using var second = _fixture.CreateClient(accessToken);
        var cart = await ReadAsync(second);

        cart.Items.Should().ContainSingle();
        cart.TotalQuantity.Should().Be(2);
        await using var db = _fixture.CreateDbContext();
        (await db.Carts.CountAsync(x => x.UserId == user.Id)).Should().Be(1, "no second cart was created");
    }

    [Fact]
    public async Task A_guest_header_alongside_a_bearer_never_diverts_an_authenticated_cart()
    {
        var product = await SeedProductAsync();
        var (user, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var strayGuest = GuestCartToken.Create();

        using var client = _fixture.CreateClient(accessToken);
        client.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", strayGuest);
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });

        await using var db = _fixture.CreateDbContext();
        (await db.Carts.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
        (await db.Carts.AnyAsync(x => x.GuestTokenHash == GuestCartToken.Hash(strayGuest)))
            .Should().BeFalse("the bearer identity always wins");
    }

    [Fact]
    public async Task Reading_a_cart_fifty_times_while_authenticated_changes_nothing()
    {
        var product = await SeedProductAsync();
        var (_, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(accessToken);
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 4 });

        for (var i = 0; i < 50; i++)
            (await ReadAsync(client)).TotalQuantity.Should().Be(4);
    }

    // ---------------------------------------------------------------- concurrency

    [Fact]
    public async Task Concurrent_reads_and_a_mutation_leave_a_coherent_cart()
    {
        // A slow read must never be able to write a stale view back over a newer one.
        var product = await SeedProductAsync(stock: 50);
        var (_, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(accessToken);
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });

        var reads = Enumerable.Range(0, 10).Select(_ => ReadAsync(client)).ToList();
        var mutation = client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        await Task.WhenAll(reads.Cast<Task>().Append(mutation));

        var final = await ReadAsync(client);
        final.Items.Should().ContainSingle();
        final.TotalQuantity.Should().Be(2, "the reads did not overwrite the mutation");
    }

    // ---------------------------------------------------------------- merge

    [Fact]
    public async Task Merging_moves_the_guest_cart_once_and_only_after_it_has_succeeded()
    {
        var product = await SeedProductAsync();
        var (token, _) = await GuestWithCartAsync(product.Id, quantity: 2);
        var (user, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");

        using var client = _fixture.CreateClient(accessToken);
        var merge = await client.PostAsJsonAsync("/api/cart/merge-guest",
            new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(token));
        merge.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await ReadAsync(client);
        cart.Items.Should().ContainSingle();
        cart.TotalQuantity.Should().Be(2, "quantities are moved, not doubled");

        await using var db = _fixture.CreateDbContext();
        (await db.Carts.AnyAsync(x => x.GuestTokenHash == GuestCartToken.Hash(token)))
            .Should().BeFalse("the source is cleaned up only after the merge succeeded");
        (await db.Carts.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A_repeated_merge_does_not_double_the_quantities()
    {
        var product = await SeedProductAsync();
        var (token, _) = await GuestWithCartAsync(product.Id, quantity: 2);
        var (_, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");

        using var client = _fixture.CreateClient(accessToken);
        var request = new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(token);
        await client.PostAsJsonAsync("/api/cart/merge-guest", request);
        await client.PostAsJsonAsync("/api/cart/merge-guest", request);

        (await ReadAsync(client)).TotalQuantity.Should().Be(2);
    }

    [Fact]
    public async Task One_guest_cart_never_leaks_into_a_second_customer()
    {
        var product = await SeedProductAsync();
        var (token, _) = await GuestWithCartAsync(product.Id, quantity: 1);
        var (_, firstToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, secondToken) = await _fixture.CreateUserAndTokenAsync("Customer");

        using var first = _fixture.CreateClient(firstToken);
        await first.PostAsJsonAsync("/api/cart/merge-guest",
            new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(token));

        using var second = _fixture.CreateClient(secondToken);
        (await ReadAsync(second)).Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<CartDto> ReadAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/cart");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
    }

    private async Task<(string Token, HttpClient Client)> GuestWithCartAsync(Guid productId, int quantity)
    {
        var token = GuestCartToken.Create();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", token);
        var add = await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = productId, Quantity = quantity });
        add.EnsureSuccessStatusCode();
        return (token, client);
    }

    private async Task<Product> SeedProductAsync(int stock = 20)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "cartpers", Slug = $"cartpers-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Cart persistence product",
            Slug = $"cartpers-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual,
            BasePrice = 12_000m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.WithCanonicalVariant(stock);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
