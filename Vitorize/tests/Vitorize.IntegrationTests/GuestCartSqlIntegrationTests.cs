using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Api.Controllers;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class GuestCartSqlIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public GuestCartSqlIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Guest_capability_persists_cart_and_authenticated_merge_is_protected_and_lossless()
    {
        var product = await SeedProductAsync();
        var guestToken = GuestCartToken.Create();
        using var guestClient = _fixture.CreateClient();
        guestClient.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", guestToken);

        var add = await guestClient.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        var added = await add.Content.ReadFromJsonAsync<ApiResult<CartDto>>();
        added!.Data!.TotalQuantity.Should().Be(2);

        var reload = await guestClient.GetFromJsonAsync<ApiResult<CartDto>>("/api/cart");
        reload!.Data!.TotalQuantity.Should().Be(2);

        using var attacker = _fixture.CreateClient();
        attacker.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", GuestCartToken.Create());
        var otherCart = await attacker.GetFromJsonAsync<ApiResult<CartDto>>("/api/cart");
        otherCart!.Data!.Items.Should().BeEmpty();

        using var unauthenticated = _fixture.CreateClient();
        var blockedMerge = await unauthenticated.PostAsJsonAsync("/api/cart/merge-guest",
            new CartController.MergeGuestCartRequest(guestToken));
        blockedMerge.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (user, accessToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var authenticated = _fixture.CreateClient(accessToken);
        var userAdd = await authenticated.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        userAdd.StatusCode.Should().Be(HttpStatusCode.OK);

        var merge = await authenticated.PostAsJsonAsync("/api/cart/merge-guest",
            new CartController.MergeGuestCartRequest(guestToken));
        merge.StatusCode.Should().Be(HttpStatusCode.OK);
        var merged = await merge.Content.ReadFromJsonAsync<ApiResult<CartDto>>();
        merged!.Data!.TotalQuantity.Should().Be(3);
        merged.Data.Items.Should().ContainSingle();

        await using var verify = _fixture.CreateDbContext();
        var userCart = await verify.Carts.Include(x => x.CartItems).SingleAsync(x => x.UserId == user.Id);
        userCart.CartItems.Single().Quantity.Should().Be(3);
        (await verify.Carts.AnyAsync(x => x.GuestTokenHash == GuestCartToken.Hash(guestToken))).Should().BeFalse();
        (await verify.Carts.Where(x => x.UserId == user.Id).Select(x => x.GuestTokenHash).SingleAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Merge_failure_after_transaction_begins_rolls_back_and_a_retry_is_lossless()
    {
        var product = await SeedProductAsync();
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var guestToken = GuestCartToken.Create();
        var guestHash = GuestCartToken.Hash(guestToken);
        var guestCartId = Guid.NewGuid();
        var userCartId = Guid.NewGuid();
        var matchFingerprint = "MATCH";
        var guestOnlyFingerprint = "GUEST-ONLY";

        await using (var seed = _fixture.CreateDbContext())
        {
            var guestCart = new Cart { Id = guestCartId, GuestTokenHash = guestHash, CreatedAt = DateTime.UtcNow, LastActivityAt = DateTime.UtcNow };
            var userCart = new Cart { Id = userCartId, UserId = user.Id, CreatedAt = DateTime.UtcNow };
            var guestMatch = CartItem(guestCartId, product.Id, matchFingerprint, 1, "guest-match");
            var guestOnly = CartItem(guestCartId, product.Id, guestOnlyFingerprint, 1, "guest-only");
            var userMatch = CartItem(userCartId, product.Id, matchFingerprint, 2, "user-match");
            seed.AddRange(guestCart, userCart, guestMatch, guestOnly, userMatch);
            await seed.SaveChangesAsync();
        }

        var interceptor = new ThrowOnceSaveInterceptor();
        var options = new DbContextOptionsBuilder<Vitorize.Infrastructure.Persistence.VitorizeDbContext>()
            .UseSqlServer(_fixture.ConnectionString).AddInterceptors(interceptor).Options;
        var crypto = _fixture.Factory.Services.GetRequiredService<Vitorize.Application.Interfaces.IEncryptionService>();
        await using (var failing = new Vitorize.Infrastructure.Persistence.VitorizeDbContext(options))
        {
            var act = () => new Vitorize.Infrastructure.Services.CartService(failing, crypto, new Vitorize.Infrastructure.Services.VatSettingsProvider(failing)).MergeGuestCartAsync(user.Id, guestToken);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        await using (var verify = _fixture.CreateDbContext())
        {
            (await verify.Carts.CountAsync(x => x.Id == guestCartId || x.Id == userCartId)).Should().Be(2);
            (await verify.CartItems.Where(x => x.CartId == guestCartId).Select(x => x.Quantity).ToListAsync()).Should().Equal(1, 1);
            (await verify.CartItems.Where(x => x.CartId == userCartId).Select(x => x.Quantity).ToListAsync()).Should().Equal(2);
            (await verify.CartItemInputValues.CountAsync()).Should().BeGreaterOrEqualTo(3);
        }

        await using (var retry = _fixture.CreateDbContext())
            (await new Vitorize.Infrastructure.Services.CartService(retry, crypto, new Vitorize.Infrastructure.Services.VatSettingsProvider(retry)).MergeGuestCartAsync(user.Id, guestToken)).TotalQuantity.Should().Be(4);

        await using var final = _fixture.CreateDbContext();
        (await final.Carts.AnyAsync(x => x.Id == guestCartId)).Should().BeFalse();
        var finalItems = await final.CartItems.Include(x => x.InputValues).Where(x => x.CartId == userCartId).ToListAsync();
        finalItems.Should().HaveCount(2);
        finalItems.Single(x => x.InputFingerprint == matchFingerprint).Quantity.Should().Be(3);
        finalItems.Should().OnlyContain(x => x.InputValues.Count == 1);
    }

    private static CartItem CartItem(Guid cartId, Guid productId, string fingerprint, int quantity, string value)
    {
        var itemId = Guid.NewGuid();
        return new CartItem
        {
            Id = itemId, CartId = cartId, ProductId = productId, InputFingerprint = fingerprint,
            Quantity = quantity, UnitPrice = 100m, CurrencyType = 2, CreatedAt = DateTime.UtcNow,
            InputValues = [new CartItemInputValue { Id = Guid.NewGuid(), CartItemId = itemId, FieldKey = "reference", FieldLabel = "Reference", FieldType = 1, Value = value, CreatedAt = DateTime.UtcNow }]
        };
    }

    private sealed class ThrowOnceSaveInterceptor : SaveChangesInterceptor
    {
        private int _thrown;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Interlocked.Exchange(ref _thrown, 1) == 0
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Deterministic merge failure"))
                : ValueTask.FromResult(result);
    }

    private async Task<Product> SeedProductAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Guest cart SQL", Slug = $"guest-cart-sql-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Guest cart product",
            Slug = $"guest-cart-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2,
            BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.WithCanonicalVariant();
        await using var db = _fixture.CreateDbContext();
        db.AddRange(category, product);
        await db.SaveChangesAsync();
        return product;
    }
}
