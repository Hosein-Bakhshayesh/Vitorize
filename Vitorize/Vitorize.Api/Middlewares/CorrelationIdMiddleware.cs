using System.Diagnostics;
using Serilog.Context;
using Vitorize.Shared.Logging;

namespace Vitorize.Api.Middlewares;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationIdPolicy.HeaderName].FirstOrDefault();
        var correlationId = CorrelationIdPolicy.Resolve(incoming);
        var previous = CorrelationContext.Current;
        CorrelationContext.Current = correlationId;
        context.Items[CorrelationIdPolicy.HeaderName] = correlationId;
        context.Response.Headers[CorrelationIdPolicy.HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdPolicy.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag("vitorize.correlation_id", correlationId);
        Activity.Current?.AddBaggage("correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            try { await _next(context); }
            finally { CorrelationContext.Current = previous; }
        }
    }
}
