using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Components;
using Vitorize.Web.Endpoints;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.UI;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App با رندر تعاملی سمت سرور
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
            if (hasAdmin && !hasCustomer)
                return VitorizeAuthSchemes.AdminScheme;

            return VitorizeAuthSchemes.CustomerScheme;
        };
    })
    .AddCookie(VitorizeAuthSchemes.AdminScheme, options =>
    {
        options.Cookie.Name = VitorizeAuthSchemes.AdminAuthCookie;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
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
});

builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();
builder.Services.AddScoped<MediaUrlResolver>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<StoreBrandingService>();
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error/500", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

// صفحات وضعیت برنددار: پاسخ‌های ۴۰۰–۵۹۹ بدون بدنه به /error/{code} بازاجرا می‌شوند
// (۴۰۳/۴۰۱/۴۰۰/۵۰۰ ...). صفحه‌ی Catch-all همچنان ۴۰۴ مسیرهای ناموجود را پوشش می‌دهد.
app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseStaticFiles();

// UseRouting صریح بعد از StaticFiles: صفحه‌ی Catch-all (۴۰۴) نباید فایل‌های استاتیک را ببلعد.
// بدون این خط، Routing خودکارِ ابتدای Pipeline مسیر فایل‌ها را به Endpoint صفحه‌ی ۴۰۴ می‌داد
// و StaticFiles (که Endpoint-aware است) از سرو کردن فایل صرف‌نظر می‌کرد.
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapAdminAuthEndpoints();
app.MapCustomerAuthEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
