using System.Text.Json;
using Vitorize.Application.Interfaces;

namespace Vitorize.Api.Middlewares
{
    /// <summary>
    /// Refuses the endpoints a customer would use to buy something while maintenance mode is on.
    ///
    /// This is where maintenance is actually enforced. Hiding buttons in the storefront is not
    /// enforcement — a customer with a page already open, or anyone calling the API directly, can
    /// still create carts, place orders and start payments. Those are the endpoints listed below, and
    /// they are refused here regardless of how the caller reached them.
    ///
    /// Two exceptions matter more than the rest:
    ///
    ///   * <b>The Zarinpal callback stays open.</b> It is the single verify endpoint for both order
    ///     payments and wallet top-ups. Blocking it would take money from a customer and never confirm
    ///     the order — the one failure this feature must not cause. Reconciliation stays open for the
    ///     same reason.
    ///   * <b>Administrators are exempt.</b> Otherwise enabling maintenance could lock out the person
    ///     who needs to disable it.
    ///
    /// Reads are left alone: someone who already paid can still look at their order.
    /// </summary>
    public sealed class MaintenanceModeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MaintenanceModeMiddleware> _logger;

        public MaintenanceModeMiddleware(RequestDelegate next, ILogger<MaintenanceModeMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IMaintenanceStateProvider maintenance)
        {
            if (!IsPurchaseEndpoint(context.Request) || IsAdministrator(context))
            {
                await _next(context);
                return;
            }

            if (!await maintenance.IsMaintenanceModeAsync(context.RequestAborted))
            {
                await _next(context);
                return;
            }

            _logger.LogInformation(
                "Refused {Method} {Path} while maintenance mode is on. EventType={EventType}",
                context.Request.Method, context.Request.Path, "MaintenanceModeBlocked");

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                isSuccess = false,
                message = "فروشگاه در حال حاضر در حالت تعمیر و نگهداری است. لطفاً بعداً دوباره تلاش کنید.",
                errorCode = "MaintenanceMode"
            }));
        }

        private static bool IsAdministrator(HttpContext context) =>
            context.User.IsInRole("Admin") || context.User.IsInRole("SuperAdmin");

        /// <summary>
        /// The endpoints that move a purchase forward. Everything else - reads, auth, health, settings,
        /// admin - is untouched, so the list is an allow-nothing rather than a block-everything.
        /// </summary>
        internal static bool IsPurchaseEndpoint(HttpRequest request)
        {
            var path = request.Path;

            // The payment verify callback must always run: money may already have changed hands.
            if (path.StartsWithSegments("/api/payments/zarinpal", StringComparison.OrdinalIgnoreCase))
                return false;

            // Anything that only reads is harmless while the shop is closed.
            if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                return false;

            if (path.StartsWithSegments("/api/cart", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWithSegments("/api/checkout", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWithSegments("/api/payments", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWithSegments("/api/wallet", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWithSegments("/api/orders", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}
