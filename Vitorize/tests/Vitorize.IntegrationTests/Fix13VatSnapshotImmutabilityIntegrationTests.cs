using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-13 purchase-time immutability. Once an order exists, no later administrative VAT change and
/// no payment retry may alter its money, and the VAT settings endpoint stays validated and audited.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix13VatSnapshotImmutabilityIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix13VatSnapshotImmutabilityIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Changing_the_rate_afterwards_leaves_the_existing_order_untouched()
    {
        await SetVatAsync(true, 10m, VatCalculationMode.BeforeDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(1_000m);

        var orderA = await CheckoutAsync(token, product.Id);
        orderA.VatRatePercent.Should().Be(10m);
        orderA.VatAmount.Should().Be(100m);
        orderA.FinalAmount.Should().Be(1_100m);

        await SetVatAsync(true, 20m, VatCalculationMode.BeforeDiscount);

        await AssertUnchangedAsync(orderA, token);

        var orderB = await CheckoutAsync(token, product.Id);
        orderB.VatRatePercent.Should().Be(20m, "only new orders pick up the new rate");
        orderB.VatAmount.Should().Be(200m);
        orderB.FinalAmount.Should().Be(1_200m);
    }

    [Fact]
    public async Task Changing_the_calculation_mode_afterwards_leaves_the_existing_order_untouched()
    {
        await SetVatAsync(true, 10m, VatCalculationMode.BeforeDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(1_000m);
        var couponCode = await CreateCouponAsync((byte)DiscountType.Percentage, 10m);

        var orderA = await CheckoutAsync(token, product.Id, couponCode);
        orderA.VatCalculationMode.Should().Be((byte)VatCalculationMode.BeforeDiscount);
        orderA.VatTaxableAmount.Should().Be(1_000m);
        orderA.FinalAmount.Should().Be(1_000m);

        await SetVatAsync(true, 10m, VatCalculationMode.AfterDiscount);

        await AssertUnchangedAsync(orderA, token);

        var orderB = await CheckoutAsync(token, product.Id, couponCode);
        orderB.VatCalculationMode.Should().Be((byte)VatCalculationMode.AfterDiscount);
        orderB.VatTaxableAmount.Should().Be(900m);
        orderB.FinalAmount.Should().Be(990m);
    }

    [Fact]
    public async Task Disabling_vat_afterwards_leaves_the_existing_order_payable_and_retryable_unchanged()
    {
        await SetVatAsync(true, 10m, VatCalculationMode.BeforeDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(1_000m);

        var orderA = await CheckoutAsync(token, product.Id);
        orderA.FinalAmount.Should().Be(1_100m);

        // A failed first attempt, so the order is genuinely retryable.
        using var client = _fixture.CreateClient(token);
        (await client.PostAsync($"/api/payments/start/{orderA.OrderId}", null)).EnsureSuccessStatusCode();
        await FailPendingAttemptsAsync(orderA.OrderId);

        await SetVatAsync(false);

        await AssertUnchangedAsync(orderA, token);

        var retry = await client.PostAsync($"/api/payments/retry/{orderA.OrderId}", null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());

        await using var db = _fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(x => x.Id == orderA.OrderId);
        order.FinalAmount.Should().Be(1_100m, "retry must never reprice from current settings");
        order.VatEnabled.Should().BeTrue();
        order.VatRatePercent.Should().Be(10m);
        (await db.Payments.Where(x => x.OrderId == order.Id).ToListAsync())
            .Should().OnlyContain(x => x.Amount == 1_100m);

        var orderB = await CheckoutAsync(token, product.Id);
        orderB.VatEnabled.Should().BeFalse("new orders follow the disabled configuration");
        orderB.VatAmount.Should().Be(0m);
        orderB.FinalAmount.Should().Be(1_000m);
    }

    [Fact]
    public async Task A_pre_fix13_order_row_reports_no_vat_and_keeps_its_original_total()
    {
        await SetVatAsync(true, 25m, VatCalculationMode.AfterDiscount);
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");

        // A historical row exactly as V0016 leaves it: column defaults, no backfill.
        var orderId = Guid.NewGuid();
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(new Order
            {
                Id = orderId, UserId = user.Id, OrderNumber = $"LEGACY-{Guid.NewGuid():N}"[..20],
                Status = (byte)OrderStatus.Completed, PaymentStatus = (byte)PaymentStatus.Paid,
                SubtotalAmount = 800m, DiscountAmount = 50m, FinalAmount = 750m,
                CurrencyType = (byte)CurrencyType.Toman, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient(token);
        var response = await client.GetAsync($"/api/orders/{orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = (await response.Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!;

        dto.VatEnabled.Should().BeFalse("current settings must never be applied to a historical order");
        dto.VatRatePercent.Should().Be(0m);
        dto.VatAmount.Should().Be(0m);
        dto.VatTaxableAmount.Should().Be(0m);
        dto.FinalAmount.Should().Be(750m);
    }

    [Fact]
    public async Task Vat_settings_are_validated_and_audited_through_the_admin_endpoint()
    {
        var (admin, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(adminToken);

        foreach (var invalid in new[] { "-1", "101", "abc" })
        {
            var rejected = await client.PostAsJsonAsync("/api/admin/settings", new UpdateSettingDto
            {
                Key = VatSettings.Keys.RatePercent, Value = invalid, GroupName = VatSettings.Group, ValueType = "decimal"
            });
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"rate '{invalid}' must be refused");
        }

        var badMode = await client.PostAsJsonAsync("/api/admin/settings", new UpdateSettingDto
        {
            Key = VatSettings.Keys.CalculationMode, Value = "Sideways", GroupName = VatSettings.Group, ValueType = "vatmode"
        });
        badMode.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetVatAsync(true, 10m, VatCalculationMode.BeforeDiscount);
        var before = DateTime.UtcNow.AddSeconds(-5);
        var accepted = await client.PostAsJsonAsync("/api/admin/settings", new UpdateSettingDto
        {
            Key = VatSettings.Keys.RatePercent, Value = "12", GroupName = VatSettings.Group, ValueType = "decimal"
        });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());

        await using var db = _fixture.CreateDbContext();
        var log = await db.AuditLogs
            .Where(x => x.EntityId == VatSettings.Keys.RatePercent && x.CreatedAt >= before)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        log.Should().NotBeNull("a VAT settings change must be auditable");
        log!.UserId.Should().Be(admin.Id);
        log.ActionType.Should().Be("SettingUpdated");
        log.Data.Should().Contain("old=10").And.Contain("new=12");
    }

    [Fact]
    public async Task A_customer_cannot_change_vat_settings()
    {
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(customerToken);

        var response = await client.PostAsJsonAsync("/api/admin/settings", new UpdateSettingDto
        {
            Key = VatSettings.Keys.RatePercent, Value = "0", GroupName = VatSettings.Group, ValueType = "decimal"
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Vat_settings_are_never_exposed_through_the_public_settings_endpoint()
    {
        await SetVatAsync(true, 10m, VatCalculationMode.AfterDiscount);
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/settings/public");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = (await response.Content.ReadFromJsonAsync<ApiResult<List<SettingDto>>>())!.Data!;

        settings.Should().NotContain(x => VatSettings.IsVatKey(x.Key));
        settings.Should().NotContain(x => x.GroupName == VatSettings.Group);
    }

    private async Task AssertUnchangedAsync(CheckoutResultDto original, string token)
    {
        await using var db = _fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(x => x.Id == original.OrderId);
        order.VatEnabled.Should().Be(original.VatEnabled);
        order.VatRatePercent.Should().Be(original.VatRatePercent);
        order.VatCalculationMode.Should().Be(original.VatCalculationMode);
        order.VatTaxableAmount.Should().Be(original.VatTaxableAmount);
        order.VatAmount.Should().Be(original.VatAmount);
        order.FinalAmount.Should().Be(original.FinalAmount);

        using var client = _fixture.CreateClient(token);
        var dto = (await (await client.GetAsync($"/api/orders/{original.OrderId}"))
            .Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!;
        dto.VatRatePercent.Should().Be(original.VatRatePercent);
        dto.VatCalculationMode.Should().Be(original.VatCalculationMode);
        dto.VatAmount.Should().Be(original.VatAmount);
        dto.FinalAmount.Should().Be(original.FinalAmount);
    }

    private async Task FailPendingAttemptsAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        foreach (var payment in await db.Payments
                     .Where(x => x.OrderId == orderId && x.Status == (byte)PaymentStatus.Pending).ToListAsync())
        {
            payment.Status = (byte)PaymentStatus.Failed;
            payment.ProviderStatusCode = "NOK";
            payment.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private async Task<CheckoutResultDto> CheckoutAsync(string token, Guid productId, string? couponCode = null)
    {
        using var client = _fixture.CreateClient(token);
        (await client.DeleteAsync("/api/cart/clear")).EnsureSuccessStatusCode();
        var added = await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = productId, Quantity = 1 });
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-snap-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = couponCode });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
    }

    private async Task SetVatAsync(bool enabled, decimal ratePercent = 0m,
        VatCalculationMode mode = VatCalculationMode.BeforeDiscount)
    {
        await using var db = _fixture.CreateDbContext();
        await UpsertAsync(db, VatSettings.Keys.Enabled, enabled ? "true" : "false", "bool");
        await UpsertAsync(db, VatSettings.Keys.RatePercent,
            ratePercent.ToString(System.Globalization.CultureInfo.InvariantCulture), "decimal");
        await UpsertAsync(db, VatSettings.Keys.CalculationMode, VatSettings.ToSettingValue(mode), "vatmode");
        await db.SaveChangesAsync();
    }

    private static async Task UpsertAsync(Vitorize.Infrastructure.Persistence.VitorizeDbContext db,
        string key, string value, string valueType)
    {
        var setting = await db.Settings.SingleOrDefaultAsync(x => x.Key == key);
        if (setting is null)
        {
            setting = new Setting { Id = Guid.NewGuid(), Key = key, GroupName = VatSettings.Group, ValueType = valueType };
            db.Settings.Add(setting);
        }
        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> CreateCouponAsync(byte discountType, decimal discountValue)
    {
        await using var db = _fixture.CreateDbContext();
        var code = $"FIX13S{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        db.Coupons.Add(new Coupon
        {
            Id = Guid.NewGuid(), Code = code, Title = "FIX-13 snapshot coupon",
            DiscountType = discountType, DiscountValue = discountValue,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return code;
    }

    private async Task<Product> CreateProductAsync(decimal price)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "FIX-13 snapshot category", Slug = $"fix13s-{Guid.NewGuid():N}",
            SortOrder = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX-13 snapshot product",
            Slug = $"fix13s-product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = price,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.WithCanonicalVariant();
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
