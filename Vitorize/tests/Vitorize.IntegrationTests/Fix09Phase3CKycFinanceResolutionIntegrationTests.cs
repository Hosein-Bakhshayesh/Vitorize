using FluentAssertions;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase3CKycFinanceResolutionIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase3CKycFinanceResolutionIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Final_rejection_creates_one_pending_item_case_and_explicit_resolution_never_refunds_the_order()
    {
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (finance, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var now = DateTime.UtcNow;
        Guid itemId;
        await using (var db = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Finance KYC", Slug = $"finance-kyc-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"finance-kyc-{Guid.NewGuid():N}", Name = "Finance KYC", IsActive = true, CreatedAt = now };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Finance KYC", CustomerActionDeadlineHours = 24, CreatedAt = now, PublishedAt = now };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Finance KYC", Slug = $"finance-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var order = new Order { Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"F-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 100, FinalAmount = 100, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
            var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = 1, UnitPrice = 100, TotalPrice = 100, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.Instant, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycPolicyVersionId = version.Id, KycCustomerActionDeadlineHours = 24, CreatedAt = now };
            db.AddRange(category, policy, version, product, order, item, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.Expired, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync(); itemId = item.Id;
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IOrderItemKycDeadlineService>().FinalRejectExpiredAsync(itemId, finance.Id);

        await using (var verify = _fixture.CreateDbContext())
        {
            (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == itemId)).Status.Should().Be((byte)OrderItemKycStatus.FinalRejected);
            (await verify.OrderItemKycFinanceResolutions.SingleAsync(x => x.OrderItemId == itemId)).Status.Should().Be((byte)OrderItemKycFinanceResolutionStatus.Pending);
            (await verify.WalletTransactions.CountAsync(x => x.UserId == customer.Id)).Should().Be(0);
            (await verify.Orders.SingleAsync(x => x.UserId == customer.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IOrderItemKycFinanceResolutionService>();
            var resolved = await service.ResolveNoRefundAsync(itemId, finance.Id, new ResolveOrderItemKycFinanceRequestDto { Reason = "Reviewed by finance" });
            resolved.Status.Should().Be((byte)OrderItemKycFinanceResolutionStatus.ResolvedNoRefund);
            Func<Task> conflicting = () => service.ResolveExternalAsync(itemId, finance.Id, new ResolveOrderItemKycFinanceRequestDto { Reason = "late", ExternalReference = "EXT-1" });
            await conflicting.Should().ThrowAsync<ConcurrencyConflictException>();
        }
    }

    [Fact]
    public async Task Mixed_order_and_two_admin_resolution_race_are_item_scoped_and_single_winner()
    {
        var (customer, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (viewer, viewerToken) = await _fixture.CreateUserAndTokenAsync("KycViewer");
        var (finance, financeToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var now = DateTime.UtcNow; Guid heldId; Guid deliveredId; Guid orderId;
        await using (var db = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Mixed finance", Slug = $"mixed-finance-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"mixed-finance-{Guid.NewGuid():N}", Name = "Mixed finance", IsActive = true, CreatedAt = now };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Mixed finance", CustomerActionDeadlineHours = 24, CreatedAt = now, PublishedAt = now };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Mixed", Slug = $"mixed-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 10, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var order = new Order { Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"M-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 20, FinalAmount = 20, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
            var delivered = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = "Delivered", Quantity = 1, UnitPrice = 10, TotalPrice = 10, CurrencyType = 2, DeliveryType = 1, DeliveryStatus = (byte)DeliveryStatus.Delivered, CreatedAt = now, DeliveredAt = now };
            var held = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = "Held", Quantity = 1, UnitPrice = 10, TotalPrice = 10, CurrencyType = 2, DeliveryType = 1, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycPolicyVersionId = version.Id, KycCustomerActionDeadlineHours = 24, CreatedAt = now };
            db.AddRange(category, policy, version, product, order, delivered, held, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = held.Id, Status = (byte)OrderItemKycStatus.Expired, CreatedAt = now, UpdatedAt = now }); await db.SaveChangesAsync(); heldId = held.Id; deliveredId = delivered.Id; orderId = order.Id;
        }
        using (var scope = _fixture.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderItemKycDeadlineService>().FinalRejectExpiredAsync(heldId, finance.Id);
        using var deniedCustomer = _fixture.CreateClient(customerToken); using var deniedViewer = _fixture.CreateClient(viewerToken);
        (await deniedCustomer.PostAsJsonAsync($"/api/admin/kyc-finance/order-items/{heldId}/no-refund", new ResolveOrderItemKycFinanceRequestDto { Reason = "x" })).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        (await deniedViewer.PostAsJsonAsync($"/api/admin/kyc-finance/order-items/{heldId}/no-refund", new ResolveOrderItemKycFinanceRequestDto { Reason = "x" })).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        using var first = _fixture.CreateClient(financeToken); using var second = _fixture.CreateClient(financeToken); using var gate = new Barrier(2);
        var external = Task.Run(async () => { gate.SignalAndWait(); return await first.PostAsJsonAsync($"/api/admin/kyc-finance/order-items/{heldId}/external-refund", new ResolveOrderItemKycFinanceRequestDto { Reason = "external", ExternalReference = "EXT-1" }); });
        var none = Task.Run(async () => { gate.SignalAndWait(); return await second.PostAsJsonAsync($"/api/admin/kyc-finance/order-items/{heldId}/no-refund", new ResolveOrderItemKycFinanceRequestDto { Reason = "none" }); });
        var results = await Task.WhenAll(external, none); results.Count(x => x.IsSuccessStatusCode).Should().Be(1); results.Count(x => x.StatusCode == System.Net.HttpStatusCode.Conflict).Should().Be(1);
        await using var verify = _fixture.CreateDbContext(); (await verify.OrderItemKycFinanceResolutions.Where(x => x.OrderItemId == heldId).ToListAsync()).Should().ContainSingle(); (await verify.OrderItems.SingleAsync(x => x.Id == deliveredId)).DeliveryStatus.Should().Be((byte)DeliveryStatus.Delivered); (await verify.Orders.SingleAsync(x => x.Id == orderId)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid); (await verify.PaymentRefunds.CountAsync(x => x.OrderId == orderId)).Should().Be(0);
    }
}
