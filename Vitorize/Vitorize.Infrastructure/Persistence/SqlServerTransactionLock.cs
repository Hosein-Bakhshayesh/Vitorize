using Microsoft.EntityFrameworkCore;

namespace Vitorize.Infrastructure.Persistence;

internal static class SqlServerTransactionLock
{
    public static async Task AcquireAsync(
        VitorizeDbContext dbContext,
        string resource,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A transaction-scoped application lock requires an active transaction.");

        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
            return;

        var safeResource = resource.Length <= 240 ? resource : resource[..240];
        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {safeResource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            IF @result < 0
                THROW 51000, 'Could not acquire the required financial transaction lock.', 1;", cancellationToken);
    }
}
