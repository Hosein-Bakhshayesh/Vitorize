using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Components;
using Vitorize.Web.Endpoints;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.UI;
using Vitorize.Shared.Common;
using System.IO.Compression;
using Serilog;
using Vitorize.Shared.Logging;
using Vitorize.Web.Logging;

Log.Logger = SerilogHostConfiguration.CreateBootstrapLogger();
try
{
Log.ForContext("EventType", "ApplicationBootstrapStarted")
    .Information("Vitorize Web bootstrap starting");

// Keep the local multi-project launch on the Development environment (see the API Program.cs note):
// when a debugger is attached and no environment was explicitly chosen, default to Development so the
// Development configuration and secret sources apply. Production hosts run without a debugger.
if (System.Diagnostics.Debugger.IsAttached &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
}

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(SerilogHostConfiguration.Configure);
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Blazor Web App با رندر تعاملی سمت سرور
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
        options.DetailedErrors = builder.Environment.IsEnvironment("Testing"))
    // A content-rich prerendered storefront can legitimately send more than SignalR's
    // 32 KiB default when the browser starts its interactive circuit. Keep the limit
    // bounded while allowing the home and product pages to hydrate reliably.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 256 * 1024);

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(x => x.Level = CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(x => x.Level = CompressionLevel.Fastest);
builder.Services.AddMemoryCache();
builder.Services.Configure<SeqOptions>(builder.Configuration.GetSection("Seq"));
builder.Services.AddHostedService<SeqConnectivityProbe>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// دو حوزه‌ی احراز هویت مجزا: ادمین و مشتری.
// طرح هوشمند بر اساس مسیر تصمیم می‌گیرد کدام کوکی استفاده شود.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = VitorizeAuthSchemes.SmartScheme;
        options.DefaultSignInScheme = VitorizeAuthSchemes.CustomerScheme;
    })
    .AddPolicyScheme(VitorizeAuthSchemes.SmartScheme, VitorizeAuthSchemes.SmartScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // Admin panel pages resolve to the admin cookie by path.
            if (context.Request.Path.StartsWithSegments("/admin"))
                return VitorizeAuthSchemes.AdminScheme;

            // The Blazor interactive circuit (and other framework endpoints such as
            // /_blazor) do NOT carry the /admin path segment, so path alone would wrongly
            // route the admin circuit to the customer scheme — that made the admin panel
            // bounce back to the login page after a successful sign-in. Decide by the page
            // that opened the connection (Referer / Origin), then fall back to whichever
            // auth cookie is actually present in the browser.
            var origin = context.Request.Headers.Referer.ToString();
            if (string.IsNullOrEmpty(origin))
                origin = context.Request.Headers.Origin.ToString();

            if (origin.Contains("/admin", StringComparison.OrdinalIgnoreCase))
                return VitorizeAuthSchemes.AdminScheme;

            var hasAdmin = context.Request.Cookies.ContainsKey(VitorizeAuthSchemes.AdminAuthCookie);
            var hasCustomer = context.Request.Cookies.ContainsKey(VitorizeAuthSchemes.CustomerAuthCookie);
            if (hasCustomer && Uri.TryCreate(origin, UriKind.Absolute, out var sourcePage) &&
                !sourcePage.AbsolutePath.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
                return VitorizeAuthSchemes.CustomerScheme;

            // When both sessions exist (for example support staff validating a
            // customer flow in the same browser), an absent Referer must not silently
            // downgrade an admin circuit to the customer identity. Explicit public
            // Referers already returned the customer scheme above.
            if (hasAdmin)
                return VitorizeAuthSchemes.AdminScheme;

            return VitorizeAuthSchemes.CustomerScheme;
        };
    })
    .AddCookie(VitorizeAuthSchemes.AdminScheme, options =>
    {
        options.Cookie.Name = VitorizeAuthSchemes.AdminAuthCookie;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddCookie(VitorizeAuthSchemes.CustomerScheme, options =>
    {
        options.Cookie.Name = VitorizeAuthSchemes.CustomerAuthCookie;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.AuthenticationSchemes.Add(VitorizeAuthSchemes.AdminScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin", "SuperAdmin");
    });

    options.AddPolicy("CustomerOnly", policy =>
    {
        policy.AuthenticationSchemes.Add(VitorizeAuthSchemes.CustomerScheme);
        policy.RequireAuthenticatedUser();
    });

    options.AddPolicy("SecurityDiagnostics", policy =>
    {
        policy.AuthenticationSchemes.Add(VitorizeAuthSchemes.AdminScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "security.diagnostics");
    });
});

builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();
builder.Services.AddScoped<MediaUrlResolver>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<StoreBrandingService>();
builder.Services.AddScoped<PrerenderApiState>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<WishlistState>();

// کلاینت API؛ آدرس پایه شامل /api/ است
var apiClientBuilder = builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("ApiSettings:BaseUrl تنظیم نشده است.");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// در محیط توسعه، گواهی self-signed لوکال API پذیرفته می‌شود
if (builder.Environment.IsDevelopment())
{
    apiClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
}

var app = builder.Build();
var webContentSecurityPolicy = SecurityHeaderPolicy.BuildWebContentSecurityPolicy(
    builder.Configuration["ApiSettings:MediaBaseUrl"]);
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error/500", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

// صفحات وضعیت برنددار: پاسخ‌های ۴۰۰–۵۹۹ بدون بدنه به /error/{code} بازاجرا می‌شوند
// (۴۰۳/۴۰۱/۴۰۰/۵۰۰ ...). صفحه‌ی Catch-all همچنان ۴۰۴ مسیرهای ناموجود را پوشش می‌دهد.
app.UseStatusCodePagesWithReExecute("/error/{0}");

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = SecurityHeaderPolicy.ContentTypeOptions;
    context.Response.Headers["X-Frame-Options"] = SecurityHeaderPolicy.WebFrameOptions;
    context.Response.Headers["Referrer-Policy"] = SecurityHeaderPolicy.ReferrerPolicy;
    context.Response.Headers["Permissions-Policy"] = SecurityHeaderPolicy.PermissionsPolicy;
    context.Response.Headers["Content-Security-Policy"] = webContentSecurityPolicy;
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public,max-age=604800,immutable";
    }
});

app.UseVitorizeRequestLogging();

// UseRouting صریح بعد از StaticFiles: صفحه‌ی Catch-all (۴۰۴) نباید فایل‌های استاتیک را ببلعد.
// بدون این خط، Routing خودکارِ ابتدای Pipeline مسیر فایل‌ها را به Endpoint صفحه‌ی ۴۰۴ می‌داد
// و StaticFiles (که Endpoint-aware است) از سرو کردن فایل صرف‌نظر می‌کرد.
app.UseRouting();

app.UseMiddleware<LegacyRedirectMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapAdminAuthEndpoints();
app.MapCustomerAuthEndpoints();
app.MapSeoEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var seqState = SerilogHostConfiguration.SeqState(app.Configuration);
if (seqState == "InvalidConfiguration")
    startupLogger.LogWarning("Seq was requested but its URL is invalid; console and file sinks remain active. EventType={EventType}", "SeqConfigurationInvalid");
else if (seqState == "Disabled")
    startupLogger.LogWarning("Seq is disabled; console and file sinks remain active. EventType={EventType}", "SeqDisabled");

app.Lifetime.ApplicationStarted.Register(() => startupLogger.LogInformation(
    "Vitorize Web started in {Environment}. EventType={EventType}", app.Environment.EnvironmentName, OperationalEventNames.ApplicationStarted));
app.Lifetime.ApplicationStopping.Register(() => startupLogger.LogInformation(
    "Vitorize Web is stopping. EventType={EventType}", OperationalEventNames.ApplicationStopping));

app.Run();
}
catch (Exception exception)
{
    Log.Fatal(
        "Vitorize Web terminated during startup. ExceptionType={ExceptionType} SafeException={SafeException} ExceptionStack={ExceptionStack} EventType={EventType}",
        exception.GetType().Name,
        SensitiveLogData.SafeExceptionMessage(exception),
        SensitiveLogData.RedactFreeText(exception.StackTrace, 8000),
        "ApplicationStartupFailed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
