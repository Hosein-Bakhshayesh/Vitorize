using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vitorize.Api.BackgroundServices;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Outbox;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SmsWorkerSeoIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public SmsWorkerSeoIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sms_outbox_handles_legacy_payload_retry_dead_letter_lease_recovery_history_and_heartbeat()
    {
        await _fixture.ConfigureSmsAsync();
        var sender = (FakeSmsSender)_fixture.Factory.Services.GetRequiredService<ISmsSender>();
        var legacyId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();

        await using (var db = _fixture.CreateDbContext())
        {
            db.OutboxMessages.AddRange(
                new OutboxMessage
                {
                    Id = legacyId, MessageType = OutboxMessageTypes.SmsSend,
                    Payload = JsonSerializer.Serialize(new SmsOutboxPayload
                    {
                        Mobile = "09350000001", TemplateKey = SmsTemplateKeys.OrderPaid, Purpose = "LegacyOrderPaid",
                        Parameters =
                        [
                            new SmsOutboxParameter { Name = "REFERENCE", Value = "VT-LEGACY-1" },
                            new SmsOutboxParameter { Name = "DETAIL", Value = "must be ignored" }
                        ]
                    }),
                    Status = (byte)OutboxMessageStatus.Pending, CreatedAt = DateTime.UtcNow.AddMinutes(-4)
                },
                new OutboxMessage
                {
                    Id = staleId, MessageType = OutboxMessageTypes.NotificationCreated, Payload = "{}",
                    Status = (byte)OutboxMessageStatus.Processing, LockedAt = DateTime.UtcNow.AddMinutes(-10),
                    LockId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddMinutes(-3)
                },
                new OutboxMessage
                {
                    Id = unknownId, MessageType = "UnknownCriticalEvent", Payload = "{}",
                    Status = (byte)OutboxMessageStatus.Pending, CreatedAt = DateTime.UtcNow.AddMinutes(-2)
                });
            await db.SaveChangesAsync();
        }

        Guid modernOutboxId;
        var modernAggregateId = Guid.NewGuid();
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Vitorize.Infrastructure.Persistence.VitorizeDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<ISmsOutboxEnqueuer>();
            await outbox.EnqueueTemplateAsync("09350000002", SmsTemplateKeys.OrderPaid,
                SmsBusinessNotificationParameters.OrderPaid("VT-RETRY-1"), "OrderPaid", modernAggregateId);
            await db.SaveChangesAsync();
            modernOutboxId = await db.OutboxMessages.Where(x => x.AggregateId == modernAggregateId)
                .Select(x => x.Id).SingleAsync();
        }

        sender.ResultsByMobile["09350000001"] = SmsSendResult.Success("legacy-success");
        sender.ResultsByMobile["09350000002"] = SmsSendResult.Failure(SmsFailureReason.Network);
        var worker = CreateWorker();
        (await InvokeOneIterationAsync(worker)).Should().BeGreaterThanOrEqualTo(4);

        await using (var db = _fixture.CreateDbContext())
        {
            (await db.OutboxMessages.FindAsync(legacyId))!.Status.Should().Be((byte)OutboxMessageStatus.Processed);
            var stale = (await db.OutboxMessages.FindAsync(staleId))!;
            stale.Status.Should().Be((byte)OutboxMessageStatus.Processed);
            stale.LockId.Should().BeNull();
            (await db.OutboxMessages.FindAsync(unknownId))!.Status.Should().Be((byte)OutboxMessageStatus.Failed);
            var retry = (await db.OutboxMessages.FindAsync(modernOutboxId))!;
            retry.Status.Should().Be((byte)OutboxMessageStatus.Pending);
            retry.RetryCount.Should().Be(1);
            retry.ProcessedAt = DateTime.UtcNow.AddMinutes(-2);
            await db.SaveChangesAsync();
        }
        var legacyCapture = sender.Sent.First(x => x.Mobile == "09350000001");
        legacyCapture.Parameters.Should().ContainSingle(x => x.Name == "ORDER_NUMBER" && x.Value == "VT-LEGACY-1");
        legacyCapture.Parameters.Should().NotContain(x => x.Name == "REFERENCE" || x.Name == "DETAIL");

        sender.ResultsByMobile["09350000002"] = SmsSendResult.Success("retry-success");
        (await InvokeOneIterationAsync(worker)).Should().BeGreaterThan(0);

        Guid deadLetterOutboxId;
        var deadAggregateId = Guid.NewGuid();
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Vitorize.Infrastructure.Persistence.VitorizeDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<ISmsOutboxEnqueuer>();
            await outbox.EnqueueTemplateAsync("09350000003", SmsTemplateKeys.OrderPaid,
                SmsBusinessNotificationParameters.OrderPaid("VT-DEAD-1"), "OrderPaid", deadAggregateId);
            await db.SaveChangesAsync();
            var dead = await db.OutboxMessages.SingleAsync(x => x.AggregateId == deadAggregateId);
            deadLetterOutboxId = dead.Id;
            dead.RetryCount = 4;
            dead.Payload = JsonSerializer.Serialize(new SmsOutboxPayload
            {
                SmsMessageId = await db.SmsMessages.Where(x => x.OutboxMessageId == dead.Id).Select(x => x.Id).SingleAsync(),
                Mobile = "09350000003", TemplateKey = SmsTemplateKeys.OrderPaid, Purpose = "InvalidContract",
                Parameters = []
            });
            await db.SaveChangesAsync();
        }
        await InvokeOneIterationAsync(worker);

        await using (var db = _fixture.CreateDbContext())
        {
            (await db.OutboxMessages.FindAsync(modernOutboxId))!.Status.Should().Be((byte)OutboxMessageStatus.Processed);
            (await db.OutboxMessages.FindAsync(deadLetterOutboxId))!.Status.Should().Be((byte)OutboxMessageStatus.Failed);
            var history = await db.SmsMessages.SingleAsync(x => x.OutboxMessageId == deadLetterOutboxId);
            history.Status.Should().Be((byte)SmsMessageStatus.DeadLetter);
            history.ProviderErrorMessage.Should().NotContain("09350000003");
        }

        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(adminToken);
        (await admin.GetAsync("/api/admin/sms?page=1&pageSize=50")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/admin/sms/summary")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
        var heartbeat = _fixture.Factory.Services.GetRequiredService<IWorkerHeartbeatRegistry>()
            .Snapshot(TimeSpan.FromMinutes(1));
        heartbeat.Should().Contain(x => x.WorkerName == nameof(OutboxProcessorBackgroundService) && x.IsHealthy);
    }

    [Fact]
    public async Task Seo_sitemaps_redirect_normalization_and_invalid_kinds_use_real_database_state()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "SEO Category", Slug = $"seo-category-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "SEO Product", Slug = $"seo-product-{Guid.NewGuid():N}",
            ProductType = (byte)ProductType.Other, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 10,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        await using (var db = _fixture.CreateDbContext())
        {
            db.Categories.Add(category); db.Products.Add(product);
            db.LegacyRedirects.Add(new LegacyRedirect
            {
                Id = Guid.NewGuid(), SourcePath = "/old-product", DestinationPath = $"/product/{product.Slug}",
                StatusCode = 301, IsActive = true, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = _fixture.CreateClient();
        var products = await client.GetAsync("/api/seo/sitemap/products?page=1&pageSize=10");
        products.StatusCode.Should().Be(HttpStatusCode.OK);
        (await products.Content.ReadAsStringAsync()).Should().Contain($"/product/{product.Slug}");
        var categories = await client.GetAsync("/api/seo/sitemap/categories");
        (await categories.Content.ReadAsStringAsync()).Should().Contain($"/category/{category.Slug}");
        var redirect = await client.GetAsync("/api/seo/redirect?path=old-product/?campaign=1");
        redirect.StatusCode.Should().Be(HttpStatusCode.OK);
        (await redirect.Content.ReadAsStringAsync()).Should().Contain($"/product/{product.Slug}").And.Contain("301");
        (await client.GetAsync("/api/seo/sitemap/unknown")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private OutboxProcessorBackgroundService CreateWorker() => new(
        _fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
        _fixture.Factory.Services.GetRequiredService<ILogger<OutboxProcessorBackgroundService>>(),
        _fixture.Factory.Services.GetRequiredService<IWorkerHeartbeatRegistry>());

    private static async Task<int> InvokeOneIterationAsync(OutboxProcessorBackgroundService worker)
    {
        var method = typeof(OutboxProcessorBackgroundService)
            .GetMethod("ProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<int>)method.Invoke(worker, [CancellationToken.None])!;
    }
}
