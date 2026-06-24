using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Filters;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Storage;
using Vitorize.Web.Services.Storefront;

namespace Vitorize.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");

                options.Conventions.AllowAnonymousToPage("/Admin/Auth/Login");
                options.Conventions.AllowAnonymousToPage("/Admin/Auth/AccessDenied");

                options.Conventions.ConfigureFilter(new AdminApiAuthorizationFilter());
            });

            builder.Services.AddHttpContextAccessor();

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = VitorizeAuthSchemes.AdminScheme;
                    options.DefaultChallengeScheme = VitorizeAuthSchemes.AdminScheme;
                })
                .AddCookie(VitorizeAuthSchemes.AdminScheme, options =>
                {
                    options.LoginPath = "/Admin/Auth/Login";
                    options.LogoutPath = "/Admin/Auth/Logout";
                    options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
                    options.Cookie.Name = "Vitorize.Admin.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                })
                .AddCookie(VitorizeAuthSchemes.CustomerScheme, options =>
                {
                    options.LoginPath = "/Auth/Login";
                    options.LogoutPath = "/Auth/Logout";
                    options.AccessDeniedPath = "/Auth/AccessDenied";
                    options.Cookie.Name = "Vitorize.Customer.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
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

            builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
            builder.Services.AddScoped<IStorefrontApiService, StorefrontApiService>();

            builder.Services.AddHttpClient<ApiClient>(client =>
            {
                var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException("ApiSettings:BaseUrl تنظیم نشده است.");

                client.BaseAddress = new Uri(baseUrl);
            });

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();
        }
    }
}