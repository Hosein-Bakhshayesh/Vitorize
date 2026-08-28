using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services;

/// <summary>Reads the two order-total KYC settings atomically with checkout.</summary>
public sealed class OrderKycSettingsProvider : IOrderKycSettingsProvider
{
    private readonly VitorizeDbContext _dbContext;

    public OrderKycSettingsProvider(VitorizeDbContext dbContext) => _dbContext = dbContext;

    public async Task<OrderKycSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await _dbContext.Settings.AsNoTracking()
            // Keep this predicate SQL-translatable. IReadOnlySet<T>.Contains is
            // not translated by EF Core's SQL Server provider.
            .Where(x => x.Key == OrderKycSettings.Keys.ThresholdToman ||
                        x.Key == OrderKycSettings.Keys.CustomerNotice)
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(cancellationToken);

        string? Value(string key) => values.FirstOrDefault(x =>
            string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

        return OrderKycSettings.Resolve(
            Value(OrderKycSettings.Keys.ThresholdToman),
            Value(OrderKycSettings.Keys.CustomerNotice));
    }
}
