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

// احراز هویت مبتنی بر کوکی برای پنل مدیریت
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = VitorizeAuthSchemes.AdminScheme;
        options.DefaultSignInScheme = VitorizeAuthSchemes.AdminScheme;
    })
    .AddCookie(VitorizeAuthSchemes.AdminScheme, options =>
    {
        options.Cookie.Name = "Vitorize.Admin.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin", "SuperAdmin");
    });
});

builder.Services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();
builder.Services.AddScoped<MediaUrlResolver>();
builder.Services.AddScoped<ToastService>();

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
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapAdminAuthEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
