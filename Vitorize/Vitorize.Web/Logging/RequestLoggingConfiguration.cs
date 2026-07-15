using System.Security.Claims;
using Serilog;
using Serilog.Events;
using Vitorize.Shared.Logging;

namespace Vitorize.Web.Logging;

public static class RequestLoggingConfiguration
{
    public static IApplicationBuilder UseVitorizeRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (context, _, exception) => MapLevel(context, exception);
            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                diagnostics.Set("EventType", "HttpRequestCompleted");
                diagnostics.Set("CorrelationId", context.Items[CorrelationIdPolicy.HeaderName]?.ToString());
                diagnostics.Set("TraceIdentifier", context.TraceIdentifier);
                diagnostics.Set("RouteName", context.GetEndpoint()?.DisplayName);
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userId, out _)) diagnostics.Set("UserId", userId);
                var roles = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value).Take(5).ToArray();
                if (roles.Length > 0) diagnostics.Set("Roles", roles);
            };
        });

    private static LogEventLevel MapLevel(HttpContext context, Exception? exception)
    {
        var path = context.Request.Path;
        var noise = path.StartsWithSegments("/_blazor") || path.StartsWithSegments("/robots.txt") ||
                    path.StartsWithSegments("/sitemap.xml") || context.Response.StatusCode == StatusCodes.Status404NotFound;
        return OperationalLogLevelPolicy.ForHttpStatus(context.Response.StatusCode, exception is not null, noise) switch
        {
            OperationalLogLevel.Debug => LogEventLevel.Debug,
            OperationalLogLevel.Warning => LogEventLevel.Warning,
            OperationalLogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };
    }
}
