using Microsoft.Extensions.Options;
using Vitorize.Shared.Logging;

namespace Vitorize.Web.Logging;

public sealed class SeqConnectivityProbe(
    IOptions<SeqOptions> options,
    ILogger<SeqConnectivityProbe> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.TryGetValidatedServer(out var server)) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            using var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(2) };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await client.GetAsync(server, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
            logger.LogInformation(
                "Seq endpoint is reachable. EventType={EventType} EndpointHost={EndpointHost}",
                "SeqConnectivitySucceeded", server!.Host);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Seq endpoint is unavailable; console and rolling-file sinks remain active. EventType={EventType} EndpointHost={EndpointHost} ExceptionType={ExceptionType}",
                "SeqConnectivityFailed", server!.Host, exception.GetType().Name);
        }
    }
}
