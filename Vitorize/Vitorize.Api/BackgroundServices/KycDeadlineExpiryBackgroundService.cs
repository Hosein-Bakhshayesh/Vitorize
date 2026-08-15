using Microsoft.Extensions.Options;
using Vitorize.Application.Interfaces;

namespace Vitorize.Api.BackgroundServices;

/// <summary>Eventually persists overdue lifecycle states; command paths remain authoritative.</summary>
public sealed class KycDeadlineExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<KycDeadlineProcessingOptions> _options;
    private readonly ILogger<KycDeadlineExpiryBackgroundService> _logger;

    public KycDeadlineExpiryBackgroundService(IServiceScopeFactory scopeFactory,
        IOptions<KycDeadlineProcessingOptions> options, ILogger<KycDeadlineExpiryBackgroundService> logger) =>
        (_scopeFactory, _options, _logger) = (scopeFactory, options, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = Validate(_options.Value);
        if (!options.Enabled)
        {
            _logger.LogInformation("KYC deadline convergence worker disabled by configuration.");
            return;
        }
        await ProcessOnceAsync(options.BatchSize, stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessOnceAsync(options.BatchSize, stoppingToken);
    }

    internal async Task<int> ProcessOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var count = await scope.ServiceProvider.GetRequiredService<IOrderItemKycDeadlineService>()
                .ProcessOverdueBatchAsync(batchSize, cancellationToken);
            if (count > 0) _logger.LogInformation("KYC deadline convergence expired {Count} item(s).", count);
            return count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "KYC deadline convergence iteration failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return 0;
        }
    }

    private static KycDeadlineProcessingOptions Validate(KycDeadlineProcessingOptions options)
    {
        if (options.IntervalSeconds <= 0 || options.BatchSize <= 0)
            throw new InvalidOperationException("KycDeadlineProcessing interval and batch size must be positive.");
        return options;
    }
}
