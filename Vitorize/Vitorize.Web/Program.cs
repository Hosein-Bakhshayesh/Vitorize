using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
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
var dataProtectionPath = builder.Configuration["Hosting:DataProtectionKeysPath"];
var dataProtectionApplicationName = builder.Configuration["Hosting:DataProtectionApplicationName"];
var trustedProxies = builder.Configuration.GetSection("Hosting:TrustedProxies").Get<string[]>() ?? [];
var trustedProxyNetworks = builder.Configuration.GetSection("Hosting:TrustedProxyNetworks").Get<string[]>() ?? [];
if (builder.Environment.IsProduction() && (string.IsNullOrWhiteSpace(dataProtectionPath) || string.IsNullOrWhiteSpace(dataProtectionApplicationName)))
    throw new InvalidOperationException("Production requires Hosting:DataProtectionKeysPath and Hosting:DataProtectionApplicationName for cookie continuity.");
if (builder.Environment.IsProduction() && trustedProxies.Length == 0 && trustedProxyNetworks.Length == 0)
    throw new InvalidOperationException("Production requires Hosting:TrustedProxies or Hosting:TrustedProxyNetworks.");
dataProtectionPath = string.IsNullOrWhiteSpace(dataProtectionPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys")
    : Path.GetFullPath(dataProtectionPath);
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName(string.IsNullOrWhiteSpace(dataProtectionApplicationName) ? "Vitorize" : dataProtectionApplicationName.Trim());
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    foreach (var proxy in trustedProxies)
    {
        if (!IPAddress.TryParse(proxy, out var address)) throw new InvalidOperationException("Hosting:TrustedProxies contains an invalid IP address.");
        options.KnownProxies.Add(address);
    }
    foreach (var network in trustedProxyNetworks)
    {
        if (!Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(network, out var parsed)) throw new InvalidOperationException("Hosting:TrustedProxyNetworks contains an invalid CIDR network.");
        options.KnownNetworks.Add(parsed);
    }
});
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
        options.ForwardDefaultSelector = context => SmartSchemeResolver.Resolve(
            context.Request.Path.Value ?? string.Empty,
            context.Request.Headers.Referer.ToString(),
            context.Request.Headers.Origin.ToString(),
            context.Request.Cookies.ContainsKey(VitorizeAuthSchemes.AdminAuthCookie),
            context.Request.Cookies.ContainsKey(VitorizeAuthSchemes.CustomerAuthCookie));
    })
    .AddCookie(VitorizeAuthSchemes.AdminScheme, options =>
    {
        options.Cookie.Name = VitorizeAuthSchemes.AdminAuthCookie;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = AuthCookiePolicy.SecurePolicy(builder.Environment);
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
        options.Cookie.SecurePolicy = AuthCookiePolicy.SecurePolicy(builder.Environment);
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
builder.Services.AddHttpClient<SessionTokenRefreshCoordinator>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? throw new InvalidOperationException("ApiSettings:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<MediaUrlResolver>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<StoreBrandingService>();
builder.Services.AddScoped<PrerenderApiState>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<WishlistState>();

// مجوز CKEditor 5: در Production کلید تجاری الزامی است و در صورت نبود/خالی برنامه
// در همان زمان راه‌اندازی با خطا متوقف می‌شود (fail fast). حالت GPL در Production
// فقط با CkEditor:AllowGplInProduction=true و همراه با هشدار مجاز است.
var ckEditorOptions = CkEditorOptions.Resolve(builder.Configuration, builder.Environment);
if (ckEditorOptions.IsGplInProduction)
{
    Log.ForContext("EventType", "CkEditorGplInProduction")
        .Warning(CkEditorOptions.GplInProductionWarning);
}
builder.Services.AddSingleton(ckEditorOptions);

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
app.UseForwardedHeaders();
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
app.MapAuthSessionEndpoints();
app.MapAdminEditorUploadEndpoints();
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
