using Microsoft.EntityFrameworkCore;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Api.BackgroundServices;

/// <summary>Low-frequency, idempotent cleanup of expired guest carts only.</summary>
public sealed class GuestCartCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GuestCartCleanupService> _logger;

    public GuestCartCleanupService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<GuestCartCleanupService> logger) =>
        (_scopeFactory, _configuration, _logger) = (scopeFactory, configuration, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CleanupAsync(stoppingToken); }
            catch (Exception exception) { _logger.LogError(exception, "GuestCartCleanupFailed EventType={EventType}", "GuestCartCleanupFailed"); }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var lifetime = Math.Clamp(_configuration.GetValue<int?>("GuestCart:LifetimeDays") ?? 30, 1, 90);
        var cutoff = DateTime.UtcNow.AddDays(-lifetime);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();
        var expired = db.Carts.Where(c => c.UserId == null && c.GuestTokenHash != null && c.LastActivityAt < cutoff);
        var ids = expired.Select(c => c.Id);
        await db.CartItemInputValues.Where(v => ids.Contains(v.CartItem.CartId)).ExecuteDeleteAsync(cancellationToken);
        await db.CartItems.Where(i => ids.Contains(i.CartId)).ExecuteDeleteAsync(cancellationToken);
        var deleted = await expired.ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0) _logger.LogInformation("GuestCartExpired Count={Count} EventType={EventType}", deleted, "GuestCartExpired");
    }
}
