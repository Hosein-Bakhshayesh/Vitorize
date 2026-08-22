using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// A successful registration must establish the same authenticated session a successful login does,
/// so nobody has to sign in a second time to use the account they just created.
///
/// The API side is proved here: the tokens registration returns are real, usable and recorded exactly
/// like login's, a failed registration produces none of them, and the guest cart is carried over
/// exactly once. The browser half - the auth cookie and the authenticated page - is covered by the
/// end-to-end run.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class RegistrationAutoLoginIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public RegistrationAutoLoginIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private const string Password = "Secure-Test-Password-123!";
    private static string UnusedMobile() => $"0912{Random.Shared.Next(1000000, 9999999)}";

    // ---------------------------------------------------------------- A, B: the session itself

    [Fact]
    public async Task Registration_returns_a_usable_access_token_without_a_second_sign_in()
    {
        using var client = _fixture.CreateClient();

        var registered = await RegisterAsync(client, UnusedMobile());

        registered.AccessToken.Should().NotBeNullOrWhiteSpace();
        registered.RefreshToken.Should().NotBeNullOrWhiteSpace();
        registered.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        registered.RefreshTokenExpiresAt.Should().BeAfter(registered.AccessTokenExpiresAt);

        // The token is immediately accepted, with no login call in between.
        using var authed = _fixture.CreateClient(registered.AccessToken);
        var me = await authed.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await me.Content.ReadFromJsonAsync<ApiResult<CurrentUserDto>>();
        body!.Data!.Id.Should().Be(registered.UserId);
    }

    [Fact]
    public async Task Registration_issues_the_same_kind_of_session_as_a_login()
    {
        // Registration and login must not drift apart: both record exactly one live refresh token and
        // grant the same default role.
        using var client = _fixture.CreateClient();
        var registered = await RegisterAsync(client, UnusedMobile());

        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.Include(x => x.Roles).SingleAsync(x => x.Id == registered.UserId);
        user.Roles.Select(x => x.Name).Should().ContainSingle("Customer");
        user.Status.Should().Be((byte)UserStatus.Active);
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == registered.UserId && x.RevokedAt == null))
            .Should().Be(1);
        // The refresh token is stored hashed, never in the clear.
        (await db.UserRefreshTokens.AnyAsync(x => x.TokenHash == registered.RefreshToken)).Should().BeFalse();
    }

    // ---------------------------------------------------------------- C, D, E: using and keeping it

    [Fact]
    public async Task A_protected_customer_endpoint_is_reachable_immediately_after_registration()
    {
        using var client = _fixture.CreateClient();
        var registered = await RegisterAsync(client, UnusedMobile());

        using var authed = _fixture.CreateClient(registered.AccessToken);

        (await authed.GetAsync("/api/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await authed.GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_session_survives_being_used_from_a_fresh_client()
    {
        // Equivalent of a reload: a brand new connection carrying the same token is still the user.
        using var client = _fixture.CreateClient();
        var registered = await RegisterAsync(client, UnusedMobile());

        using var reload = _fixture.CreateClient(registered.AccessToken);
        var me = await reload.GetAsync("/api/auth/me");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<ApiResult<CurrentUserDto>>())!.Data!.Id.Should().Be(registered.UserId);
    }

    [Fact]
    public async Task The_refresh_token_from_registration_can_rotate_the_session()
    {
        using var client = _fixture.CreateClient();
        var registered = await RegisterAsync(client, UnusedMobile());

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshTokenRequestDto { RefreshToken = registered.RefreshToken });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await refreshed.Content.ReadFromJsonAsync<ApiResult<AuthResponseDto>>();
        body!.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Data.RefreshToken.Should().NotBe(registered.RefreshToken, "rotation replaces the token");
    }

    // ---------------------------------------------------------------- F: guest cart

    [Fact]
    public async Task A_guest_cart_is_carried_into_the_new_account_exactly_once()
    {
        using var client = _fixture.CreateClient();
        var product = await SeedProductAsync();
        var guestToken = await CreateGuestCartAsync(product.Id, quantity: 2);

        var registered = await RegisterAsync(client, UnusedMobile());
        using var authed = _fixture.CreateClient(registered.AccessToken);
        var merge = await authed.PostAsJsonAsync("/api/cart/merge-guest", new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(guestToken));
        merge.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await ReadCartAsync(authed);
        cart.Items.Should().ContainSingle(x => x.ProductId == product.Id);
        cart.Items.Single(x => x.ProductId == product.Id).Quantity.Should().Be(2, "quantities must not be doubled");
    }

    [Fact]
    public async Task Merging_the_same_guest_cart_twice_does_not_duplicate_quantities()
    {
        using var client = _fixture.CreateClient();
        var product = await SeedProductAsync();
        var guestToken = await CreateGuestCartAsync(product.Id, quantity: 1);

        var registered = await RegisterAsync(client, UnusedMobile());
        using var authed = _fixture.CreateClient(registered.AccessToken);
        await authed.PostAsJsonAsync("/api/cart/merge-guest", new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(guestToken));
        await authed.PostAsJsonAsync("/api/cart/merge-guest", new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(guestToken));

        var cart = await ReadCartAsync(authed);
        cart.Items.Where(x => x.ProductId == product.Id).Should().HaveCount(1);
        cart.Items.Single(x => x.ProductId == product.Id).Quantity.Should().Be(1);
    }

    [Fact]
    public async Task One_customer_guest_cart_never_leaks_into_another_new_account()
    {
        using var client = _fixture.CreateClient();
        var product = await SeedProductAsync();
        var guestToken = await CreateGuestCartAsync(product.Id, quantity: 1);

        var first = await RegisterAsync(client, UnusedMobile());
        using var firstClient = _fixture.CreateClient(first.AccessToken);
        await firstClient.PostAsJsonAsync("/api/cart/merge-guest", new Vitorize.Api.Controllers.CartController.MergeGuestCartRequest(guestToken));

        var second = await RegisterAsync(client, UnusedMobile());
        using var secondClient = _fixture.CreateClient(second.AccessToken);

        (await ReadCartAsync(secondClient)).Items.Should().BeEmpty("a second new account starts empty");
    }

    // ---------------------------------------------------------------- H, I: failures create nothing

    [Fact]
    public async Task A_duplicate_registration_creates_no_second_account_and_no_second_session()
    {
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();
        var first = await RegisterAsync(client, mobile);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "دوباره", Mobile = mobile,
            Email = $"dup-{Guid.NewGuid():N}@example.test", Password = Password
        });

        duplicate.IsSuccessStatusCode.Should().BeFalse();
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<ApiResult<RegistrationChallengeDto>>();
        duplicateBody!.Data.Should().BeNull();
        duplicateBody.ErrorCode.Should().Be(AuthOutcomeCodes.AlreadyRegistered);
        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile && !x.IsDeleted)).Should().Be(1);
        (await db.UserRefreshTokens.CountAsync(x => x.UserId == first.UserId && x.RevokedAt == null))
            .Should().Be(1, "the rejected attempt issued nothing");
    }

    [Fact]
    public async Task A_registration_that_fails_validation_establishes_no_session_and_no_user()
    {
        using var client = _fixture.CreateClient();
        var mobile = UnusedMobile();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "", Mobile = mobile, Email = "not-an-email", Password = "x"
        });

        response.IsSuccessStatusCode.Should().BeFalse();
        await using var db = _fixture.CreateDbContext();
        (await db.Users.CountAsync(x => x.Mobile == mobile)).Should().Be(0);
        (await db.UserRefreshTokens.CountAsync(x => x.User.Mobile == mobile)).Should().Be(0);
    }

    [Fact]
    public async Task Completing_the_verified_registration_confirms_the_mobile()
    {
        // The mobile is now confirmed because a code sent to it was actually verified. Identity
        // verification (KYC) is a separate claim and deliberately stays Pending.
        using var client = _fixture.CreateClient();
        var registered = await RegisterAsync(client, UnusedMobile());

        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == registered.UserId);
        user.IsMobileConfirmed.Should().BeTrue();
        user.Status.Should().Be((byte)UserStatus.Active);
        user.VerificationStatus.Should().Be((byte)VerificationStatus.Pending, "KYC is a different claim");
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Registers through both steps of the verified flow. Registration itself no longer issues a
    /// session, so what these tests assert is the state AFTER the mobile code is verified: that is
    /// where the "no second sign-in" promise now lives.
    /// </summary>
    private async Task<AuthResponseDto> RegisterAsync(HttpClient client, string mobile)
    {
        var challenge = await StartAsync(client, mobile);
        challenge.Outcome.Should().Be(AuthOutcomeCodes.RegistrationOtpSent);
        return await _fixture.CompleteRegistrationAsync(client, mobile);
    }

    private async Task<RegistrationChallengeDto> StartAsync(HttpClient client, string mobile)
    {
        // Registration now sends a code, and the service refuses to claim it sent one when the SMS
        // provider is unusable. Configuring the fake provider per test keeps this deterministic no
        // matter what order the shared fixture runs its classes in.
        await _fixture.ConfigureSmsAsync();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = "کاربر ثبت‌نام خودکار",
            Mobile = mobile,
            Email = $"auto-{Guid.NewGuid():N}@example.test",
            Password = Password
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<RegistrationChallengeDto>>())!.Data!;
    }

    private async Task<CartDto> ReadCartAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/cart");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
    }

    /// <summary>Builds a real guest cart through the public API, exactly as a browser would.</summary>
    private async Task<string> CreateGuestCartAsync(Guid productId, int quantity)
    {
        var token = GuestCartToken.Create();
        using var guest = _fixture.CreateClient();
        guest.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", token);
        var add = await guest.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = productId, Quantity = quantity });
        add.EnsureSuccessStatusCode();
        return token;
    }

    private async Task<Product> SeedProductAsync(int stock = 10)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "autologin", Slug = $"autologin-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Auto-login product",
            Slug = $"autologin-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual,
            BasePrice = 15_000m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.WithCanonicalVariant(stock);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
