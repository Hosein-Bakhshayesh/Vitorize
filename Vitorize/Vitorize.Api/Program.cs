using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO.Compression;
using System.Text;
using Vitorize.Api.BackgroundServices;
using Vitorize.Api.Filters;
using Vitorize.Api.Middlewares;
using Vitorize.Application;
using Vitorize.Application.Common;
using Vitorize.Infrastructure;

namespace Vitorize.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers + FluentValidation filter
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
            });

            builder.Services.AddEndpointsApiExplorer();

            // Swagger فقط برای Development نمایش داده می‌شود
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Vitorize API",
                    Version = "v1"
                });

                options.EnableAnnotations();

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT Authorization header. Example: Bearer {token}"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // CORS برای اتصال Web/Razor/Frontend به API
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("VitorizeCors", policy =>
                {
                    policy
                        .WithOrigins(
                            "https://localhost:7221",
                            "http://localhost:5177",
                            "https://localhost:7002",
                            "https://localhost:7003",
                            "https://vitorize.com",
                            "https://www.vitorize.com")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            // فشرده‌سازی Response برای بهتر شدن Performance
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);

            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("Jwt"));

            var jwtSettings = builder.Configuration
                .GetSection("Jwt")
                .Get<JwtSettings>();

            if (jwtSettings == null)
            {
                throw new InvalidOperationException(
                    "Jwt settings are not configured.");
            }

            // JWT Authentication
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        JwtBearerDefaults.AuthenticationScheme;

                    options.DefaultChallengeScheme =
                        JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;

                    // روی سرور Production حتماً true بماند
                    options.RequireHttpsMetadata = true;

                    options.SaveToken = true;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

                        ValidateLifetime = true,

                        // توکن دقیقاً در زمان انقضا Expire شود
                        ClockSkew = TimeSpan.Zero
                    };
                });

            // Authorization Policies
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireRole("Admin", "SuperAdmin"));

                options.AddPolicy("SuperAdminOnly", policy =>
                    policy.RequireRole("SuperAdmin"));

                options.AddPolicy("SupportOnly", policy =>
                    policy.RequireRole("Support", "Admin", "SuperAdmin"));
            });

            // Rate Limiting برای جلوگیری از Brute Force و Spam
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddFixedWindowLimiter("login", opt =>
                {
                    opt.PermitLimit = 5;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueProcessingOrder =
                        System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                    opt.QueueLimit = 0;
                });

                options.AddFixedWindowLimiter("otp", opt =>
                {
                    opt.PermitLimit = 3;
                    opt.Window = TimeSpan.FromMinutes(1);
                });

                options.AddFixedWindowLimiter("register", opt =>
                {
                    opt.PermitLimit = 3;
                    opt.Window = TimeSpan.FromMinutes(5);
                });
            });

            // Background Services
            builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
            builder.Services.AddHostedService<BackgroundJobProcessor>();

            var app = builder.Build();

            // Global Exception Handler
            app.UseMiddleware<GlobalExceptionMiddleware>();

            // Swagger فقط در محیط Development
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // HSTS فقط در Production
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseResponseCompression();

            app.UseCors("VitorizeCors");

            app.UseStaticFiles();

            // Security Headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
                context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
                context.Response.Headers.TryAdd("X-XSS-Protection", "0");

                await next();
            });

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseRateLimiter();

            app.MapControllers();

            app.Run();
        }
    }
}