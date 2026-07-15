using System.Diagnostics;
using System.Reflection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Vitorize.Shared.Logging;

namespace Vitorize.Api.Logging;

public static class SerilogHostConfiguration
{
    public static Serilog.ILogger CreateBootstrapLogger() => new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Vitorize.Api")
        .Enrich.WithProperty("Service", "Api")
        .WriteTo.Console()
        .CreateBootstrapLogger();

    public static void Configure(
        HostBuilderContext context,
        IServiceProvider services,
        LoggerConfiguration loggerConfiguration)
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.With(new SensitivePropertyEnricher())
            .Enrich.With(new ActivityEnricher())
            .Enrich.WithProperty("InstanceId", ResolveInstanceId())
            .Enrich.WithProperty("Version", ResolveVersion());

        var seq = context.Configuration.GetSection("Seq").Get<SeqOptions>() ?? new SeqOptions();
        loggerConfiguration.Enrich.With(new ApplicationNameEnricher(
            string.IsNullOrWhiteSpace(seq.ApplicationName) ? "Vitorize.Api" : SensitiveLogData.Sanitize(seq.ApplicationName, 80)));
        if (seq.TryGetValidatedServer(out var server))
        {
            loggerConfiguration.WriteTo.Seq(
                server!.AbsoluteUri,
                apiKey: string.IsNullOrWhiteSpace(seq.ApiKey) ? null : seq.ApiKey,
                queueSizeLimit: Math.Clamp(seq.QueueSizeLimit, 100, 100_000));
        }
    }

    public static string SeqState(IConfiguration configuration)
    {
        var seq = configuration.GetSection("Seq").Get<SeqOptions>() ?? new SeqOptions();
        if (!seq.Enabled) return "Disabled";
        return seq.TryGetValidatedServer(out _) ? "Enabled" : "InvalidConfiguration";
    }

    private static string ResolveInstanceId() =>
        Environment.GetEnvironmentVariable("HOSTNAME") ??
        $"{Environment.MachineName}-{Environment.ProcessId}";

    private static string ResolveVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
}

internal sealed class ApplicationNameEnricher(string applicationName) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Application", applicationName));
}

internal sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null) return;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
    }
}

internal sealed class SensitivePropertyEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
            logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, Redact(property.Key, property.Value)));
    }

    private static LogEventPropertyValue Redact(string name, LogEventPropertyValue value)
    {
        if (SensitiveLogData.IsSensitiveProperty(name)) return new ScalarValue(SensitiveLogData.Redacted);
        return value switch
        {
            StructureValue structure => new StructureValue(
                structure.Properties.Select(p => new LogEventProperty(p.Name, Redact(p.Name, p.Value))),
                structure.TypeTag),
            DictionaryValue dictionary => new DictionaryValue(dictionary.Elements.Select(pair =>
                new KeyValuePair<ScalarValue, LogEventPropertyValue>(pair.Key, Redact(pair.Key.Value?.ToString() ?? string.Empty, pair.Value)))),
            SequenceValue sequence => new SequenceValue(sequence.Elements.Select(item => Redact(name, item))),
            ScalarValue { Value: string text } => new ScalarValue(SensitiveLogData.RedactFreeText(text)),
            _ => value
        };
    }
}
