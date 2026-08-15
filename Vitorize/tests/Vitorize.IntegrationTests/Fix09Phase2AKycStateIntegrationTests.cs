using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2AKycStateIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix09Phase2AKycStateIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Lifecycle_state_is_one_to_one_concurrency_protected_and_does_not_change_the_order()
    {
        var orderItem = await SeedOrderItemAsync();
        var stateId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.OrderItemKycStates.Add(new OrderItemKycState
            {
                Id = stateId, OrderItemId = orderItem.Id,
                Status = (byte)OrderItemKycStatus.AwaitingSubmission,
                CreatedAt = now, UpdatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        await using var first = _fixture.CreateDbContext();
        await using var stale = _fixture.CreateDbContext();
        var firstState = await first.OrderItemKycStates.SingleAsync(x => x.Id == stateId);
        var staleState = await stale.OrderItemKycStates.SingleAsync(x => x.Id == stateId);
        firstState.Status = (byte)OrderItemKycStatus.AwaitingReview;
        firstState.UpdatedAt = DateTime.UtcNow;
        await first.SaveChangesAsync();

        staleState.Status = (byte)OrderItemKycStatus.FinalRejected;
        staleState.UpdatedAt = DateTime.UtcNow;
        Func<Task> staleSave = () => stale.SaveChangesAsync();
        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.Include(x => x.KycLifecycleState).SingleAsync(x => x.Id == orderItem.Id);
        item.KycLifecycleState!.Status.Should().Be((byte)OrderItemKycStatus.AwaitingReview);
        item.KycLifecycleState.RowVersion.Should().NotBeEmpty();
        item.OrderId.Should().Be(orderItem.OrderId);
    }

    [Fact]
    public async Task SQL_Server_rejects_duplicate_and_invalid_lifecycle_references()
    {
        var orderItem = await SeedOrderItemAsync();
        var now = DateTime.UtcNow;
        await using (var duplicate = _fixture.CreateDbContext())
        {
            duplicate.OrderItemKycStates.AddRange(
                NewState(orderItem.Id, now),
                NewState(orderItem.Id, now));
            Func<Task> save = () => duplicate.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var invalidItem = _fixture.CreateDbContext())
        {
            invalidItem.OrderItemKycStates.Add(NewState(Guid.NewGuid(), now));
            Func<Task> save = () => invalidItem.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var invalidProfile = _fixture.CreateDbContext())
        {
            invalidProfile.OrderItemKycStates.Add(new OrderItemKycState
            {
                Id = Guid.NewGuid(), OrderItemId = orderItem.Id,
                Status = (byte)OrderItemKycStatus.Satisfied,
                CreatedAt = now, UpdatedAt = now, SatisfiedAt = now,
                SatisfiedByVerificationProfileId = Guid.NewGuid()
            });
            Func<Task> save = () => invalidProfile.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task V0011_upgrades_a_disposable_phase1_schema_without_backfilling_or_mutating_snapshots()
    {
        var historicalKycItem = await SeedOrderItemAsync();
        var noKycItem = await SeedOrderItemAsync();
        var legacyCompatibleItem = await SeedOrderItemAsync();
        Guid policyVersionId;
        await using (var phase1 = _fixture.CreateDbContext())
        {
            policyVersionId = await phase1.KycPolicyVersions
                .Where(x => x.KycPolicy.Code == "legacy-profile-verification")
                .Select(x => x.Id).SingleAsync();
            var phase1Item = await phase1.OrderItems.SingleAsync(x => x.Id == historicalKycItem.Id);
            phase1Item.RequiresVerification = true;
            phase1Item.KycRequirementMode = (byte)KycRequirementMode.AboveThreshold;
            phase1Item.KycThresholdAmount = 50m;
            phase1Item.KycEvaluatedAmount = 100m;
            phase1Item.KycPolicyVersionId = policyVersionId;
            var legacyItem = await phase1.OrderItems.SingleAsync(x => x.Id == legacyCompatibleItem.Id);
            legacyItem.RequiresVerification = true;
            legacyItem.KycRequirementMode = (byte)KycRequirementMode.Always;
            legacyItem.KycThresholdAmount = null;
            legacyItem.KycEvaluatedAmount = 100m;
            legacyItem.KycPolicyVersionId = policyVersionId;
            await phase1.SaveChangesAsync();

            // This isolated integration database already applied V0011 during
            // fixture setup. Removing only its additive table recreates the real
            // V0010 shape, then the exact V0011 file is executed below.
            await phase1.Database.ExecuteSqlRawAsync("DROP TABLE dbo.OrderItemKycStates");
        }

        await _fixture.RunSqlFileAsync(Path.Combine("Database", "Versioned", "V0011__order_item_kyc_lifecycle_state.sql"));

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.Include(x => x.KycLifecycleState).SingleAsync(x => x.Id == historicalKycItem.Id);
        item.RequiresVerification.Should().BeTrue();
        item.KycRequirementMode.Should().Be((byte)KycRequirementMode.AboveThreshold);
        item.KycThresholdAmount.Should().Be(50m);
        item.KycEvaluatedAmount.Should().Be(100m);
        item.KycPolicyVersionId.Should().Be(policyVersionId);
        item.KycLifecycleState.Should().BeNull("Phase 2A intentionally does not backfill ambiguous historical item KYC states");
        var untouchedNoKycItem = await verify.OrderItems.Include(x => x.KycLifecycleState).SingleAsync(x => x.Id == noKycItem.Id);
        untouchedNoKycItem.RequiresVerification.Should().BeFalse();
        untouchedNoKycItem.KycRequirementMode.Should().Be((byte)KycRequirementMode.None);
        untouchedNoKycItem.KycLifecycleState.Should().BeNull();
        var preservedLegacyItem = await verify.OrderItems.Include(x => x.KycLifecycleState).SingleAsync(x => x.Id == legacyCompatibleItem.Id);
        preservedLegacyItem.KycRequirementMode.Should().Be((byte)KycRequirementMode.Always);
        preservedLegacyItem.KycPolicyVersionId.Should().Be(policyVersionId);
        preservedLegacyItem.KycLifecycleState.Should().BeNull();

        var metadata = await verify.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value]
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            WHERE t.name = 'OrderItemKycStates' AND c.name = 'RowVersion'
            """).SingleAsync();
        metadata.Should().Be(1);
        (await verify.OrderItemKycStates.CountAsync()).Should().Be(0);
    }

    private async Task<OrderItem> SeedOrderItemAsync()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Phase 2A KYC category", Slug = $"phase2a-kyc-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Phase 2A KYC product",
            Slug = $"phase2a-kyc-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2,
            BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"P2A-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
            SubtotalAmount = 100m, DiscountAmount = 0m, FinalAmount = 100m, CurrencyType = 2, CreatedAt = DateTime.UtcNow
        };
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
            Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, CurrencyType = 2, DeliveryType = 2,
            DeliveryStatus = 1, RequiresVerification = false, KycRequirementMode = (byte)KycRequirementMode.None,
            KycEvaluatedAmount = 0m, CreatedAt = DateTime.UtcNow
        };

        await using var db = _fixture.CreateDbContext();
        db.AddRange(category, product, order, item);
        await db.SaveChangesAsync();
        return item;
    }

    private static OrderItemKycState NewState(Guid orderItemId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), OrderItemId = orderItemId,
        Status = (byte)OrderItemKycStatus.AwaitingSubmission,
        CreatedAt = now, UpdatedAt = now
    };
}
