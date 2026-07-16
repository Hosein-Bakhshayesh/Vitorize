using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Admin.Coupons;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CommerceApiExtendedIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public CommerceApiExtendedIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Coupon_admin_CRUD_and_customer_validation_apply_real_limits()
    {
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var admin = _fixture.CreateClient(adminToken);
        using var customer = _fixture.CreateClient(customerToken);
        var code = $"INT{Random.Shared.Next(100000, 999999)}";

        var coupon = await PostDataAsync<CouponDto>(admin, "/api/admin/coupons", new AdminCouponCreateDto
        {
            Code = code, Title = "Integration coupon", DiscountType = (byte)DiscountType.Percentage,
            DiscountValue = 20, MinOrderAmount = 100, MaxUsageCount = 2, MaxUsagePerUser = 1,
            StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddHours(1), IsActive = true
        });
        var valid = await PostDataAsync<ValidateCouponResultDto>(customer, "/api/coupons/validate",
            new ValidateCouponRequestDto { Code = code, OrderAmount = 500 });
        valid.DiscountAmount.Should().Be(100);
        (await customer.PostAsJsonAsync("/api/coupons/validate",
            new ValidateCouponRequestDto { Code = code, OrderAmount = 50 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/admin/coupons/{coupon.Id}", new AdminCouponUpdateDto
        {
            Title = "Updated coupon", DiscountType = (byte)DiscountType.FixedAmount, DiscountValue = 75,
            MaxUsageCount = 2, MaxUsagePerUser = 1, MinOrderAmount = 100, IsActive = true
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/admin/coupons/{coupon.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/coupons/{coupon.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.PostAsJsonAsync("/api/coupons/validate",
            new ValidateCouponRequestDto { Code = code, OrderAmount = 500 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Wallet_topup_is_idempotent_and_order_cancel_records_history_and_enforces_authorization()
    {
        var (customerUser, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (adminUser, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var customer = _fixture.CreateClient(customerToken);
        using var other = _fixture.CreateClient(otherToken);
        using var admin = _fixture.CreateClient(adminToken);

        var topup = await PostDataAsync<WalletTopUpStartResultDto>(customer, "/api/wallet/topup",
            new WalletTopUpRequestDto { Amount = 250 });
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IWalletTopUpService>();
            (await service.VerifyMockAsync(customerUser.Id, topup.TopUpId)).IsPaid.Should().BeTrue();
            (await service.VerifyMockAsync(customerUser.Id, topup.TopUpId)).IsPaid.Should().BeTrue();
        }
        await using (var db = _fixture.CreateDbContext())
        {
            (await db.Wallets.SingleAsync(x => x.UserId == customerUser.Id)).Balance.Should().Be(250);
            (await db.WalletTransactions.CountAsync(x => x.UserId == customerUser.Id)).Should().Be(1);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = customerUser.Id, OrderNumber = $"VT-CANCEL-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
            SubtotalAmount = 10, FinalAmount = 10, CreatedAt = DateTime.UtcNow
        };
        await using (var db = _fixture.CreateDbContext()) { db.Orders.Add(order); await db.SaveChangesAsync(); }
        (await other.GetAsync($"/api/orders/{order.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await customer.PostAsJsonAsync($"/api/admin/orders/{order.Id}/cancel",
            new CancelOrderRequestDto { Reason = "unauthorized" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/cancel",
            new CancelOrderRequestDto { Reason = "Integration cancellation" })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.Cancelled);
        (await verify.OrderStatusHistories.SingleAsync(x => x.OrderId == order.Id)).ChangedByUserId.Should().Be(adminUser.Id);
    }

    private static async Task<T> PostDataAsync<T>(HttpClient client, string uri, object request)
    {
        var response = await client.PostAsJsonAsync(uri, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }
}
