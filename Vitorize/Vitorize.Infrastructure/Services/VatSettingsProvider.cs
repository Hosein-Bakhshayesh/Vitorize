using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// Reads the three VAT keys from the existing Settings table in a single query. It shares the
/// scoped <see cref="VitorizeDbContext"/>, so a read issued inside the checkout transaction
/// participates in that transaction and the order snapshot is taken atomically.
/// Settings are not cached anywhere in this application, so an administrative change applies to the
/// next checkout without a restart.
/// </summary>
public sealed class VatSettingsProvider : IVatSettingsProvider
{
    private readonly VitorizeDbContext _dbContext;

    public VatSettingsProvider(VitorizeDbContext dbContext) => _dbContext = dbContext;

    public async Task<VatSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await _dbContext.Settings
            .AsNoTracking()
            .Where(x => VatSettings.Keys.All.Contains(x.Key))
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(cancellationToken);

        string? Value(string key) => values
            .FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

        return VatSettings.Resolve(
            Value(VatSettings.Keys.Enabled),
            Value(VatSettings.Keys.RatePercent),
            Value(VatSettings.Keys.CalculationMode));
    }
}
