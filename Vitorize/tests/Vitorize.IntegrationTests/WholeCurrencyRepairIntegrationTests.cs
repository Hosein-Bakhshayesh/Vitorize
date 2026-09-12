using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Xunit;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class WholeCurrencyRepairIntegrationTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Repair_preview_apply_and_repeat_preserve_paid_active_attempts_and_failed_history()
    {
        // The fixture creates a unique disposable LOCAL test database; never the production DB.
        var (user, _) = await fixture.CreateUserAndTokenAsync("Customer");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var safeId = Guid.NewGuid();
        var paidId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var initializingId = Guid.NewGuid();
        var oldFailedId = Guid.NewGuid();
        var readyId = Guid.NewGuid();
        await using (var db = fixture.CreateDbContext())
        {
            var category = new Category { Id=Guid.NewGuid(), Title="money", Slug=$"money-{productId:N}", IsActive=true };
            db.Categories.Add(category);
            db.Products.Add(new Product { Id=productId, CategoryId=category.Id, Title="money", Slug=$"money-{productId:N}",
                BasePrice=198782.10m, CurrencyType=2, DeliveryType=2, ProductType=1, MinOrderQuantity=1, IsActive=true });
            db.ProductVariants.Add(new ProductVariant { Id=variantId, ProductId=productId, Title="50", Price=198782.10m, IsActive=true });
            foreach (var id in new[] { safeId, paidId, activeId, initializingId })
            {
                db.Orders.Add(new Order { Id=id, UserId=user.Id, OrderNumber=$"repair-{id:N}", Status=1,
                    PaymentStatus=(byte)(id==paidId ? 2 : 1), CurrencyType=2,
                    SubtotalAmount=198782.10m, FinalAmount=200769.92m, VatEnabled=true, VatRatePercent=1,
                    VatCalculationMode=1, VatTaxableAmount=198782.10m, VatAmount=1987.82m, CreatedAt=DateTime.UtcNow });
                db.OrderItems.Add(new OrderItem { Id=Guid.NewGuid(), OrderId=id, ProductId=productId,
                    ProductVariantId=variantId, ProductTitle="money", Quantity=1, UnitPrice=198782.10m,
                    TotalPrice=198782.10m, CurrencyType=2, DeliveryType=2, DeliveryStatus=1, KycEvaluatedAmount=200769.92m });
            }
            foreach (var (id, order, status, code, authority) in new[] {
                (oldFailedId,safeId,3,"REQUEST_FAILED",(string?)null),
                (readyId,safeId,1,"READY",(string?)null),
                (Guid.NewGuid(),paidId,2,"VERIFIED",(string?)"paid-test"),
                (Guid.NewGuid(),activeId,1,"REQUESTED",(string?)"active-test"),
                (Guid.NewGuid(),initializingId,1,"INITIALIZING",(string?)null) })
                db.Payments.Add(new Payment { Id=id, OrderId=order, UserId=user.Id, Status=(byte)status,
                    ProviderStatusCode=code, Authority=authority, Gateway="Zarinpal", CurrencyType=2,
                    Amount=200769.92m, RequestedAt=DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var script = await File.ReadAllTextAsync(Path.Combine(fixture.RepositoryRoot, "Database", "Tools", "Repair-Whole-Currency-20260911.sql"));
        var database = new SqlConnectionStringBuilder(fixture.ConnectionString).InitialCatalog;
        Assert.StartsWith("VitorizeIntegration_", database);
        script = script.Replace("N'VitorizeDb'", $"N'{database}'");
        async Task Run(string sql)
        {
            await using var connection = new SqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout=120 };
            await command.ExecuteNonQueryAsync();
        }
        await Run(script); // default preview
        await using (var preview = fixture.CreateDbContext())
            (await preview.Orders.SingleAsync(x=>x.Id==safeId)).FinalAmount.Should().Be(200769.92m);

        var apply = script.Replace("@Apply bit = 0", "@Apply bit = 1")
            .Replace("@MaintenanceConfirmed bit = 0", "@MaintenanceConfirmed bit = 1");
        await Run(apply);
        await Run(apply); // safe repeat; never reprice history
        await using var check = fixture.CreateDbContext();
        (await check.Products.SingleAsync(x=>x.Id==productId)).BasePrice.Should().Be(198782m);
        (await check.ProductVariants.SingleAsync(x=>x.Id==variantId)).Price.Should().Be(198782m);
        var safe = await check.Orders.SingleAsync(x=>x.Id==safeId);
        safe.FinalAmount.Should().Be(200770m);
        safe.VatAmount.Should().Be(1988m);
        (await check.OrderItems.SingleAsync(x=>x.OrderId==safeId)).UnitPrice.Should().Be(198782m);
        foreach(var skipped in new[] { paidId, activeId, initializingId })
            (await check.Orders.SingleAsync(x=>x.Id==skipped)).FinalAmount.Should().Be(200769.92m);
        (await check.Payments.SingleAsync(x=>x.Id==oldFailedId)).Amount.Should().Be(200769.92m);
        (await check.Payments.SingleAsync(x=>x.Id==readyId)).Status.Should().Be(3);
        (await check.Payments.SingleAsync(x=>x.Id==readyId)).Amount.Should().Be(200769.92m);
        (await check.AuditLogs.AnyAsync(x=>x.ActionType=="WholeCurrencyRepair")).Should().BeTrue();
    }
}
