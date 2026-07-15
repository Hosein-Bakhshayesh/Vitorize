using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO.Compression;
using System.Text;
using Serilog;
using Vitorize.Api.BackgroundServices;
using Vitorize.Api.Extensions;
using Vitorize.Api.Filters;
using Vitorize.Api.Logging;
using Vitorize.Api.Middlewares;
using Vitorize.Application;
using Vitorize.Application.Common;
using Vitorize.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Logging;

namespace Vitorize.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = SerilogHostConfiguration.CreateBootstrapLogger();
            try
            {
            Log.ForContext("EventType", "ApplicationBootstrapStarted")
                .Information("Vitorize API bootstrap starting");
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog(SerilogHostConfiguration.Configure);
            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = true;
            });

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

            if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.SecretKey) ||
                Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:SecretKey must be supplied by an environment variable or secret provider and contain at least 32 bytes.");
            }

            var encryptionKey = builder.Configuration["Encryption:Key"];
            if (string.IsNullOrWhiteSpace(encryptionKey) || Encoding.UTF8.GetByteCount(encryptionKey) != 32)
                throw new InvalidOperationException(
                    "Encryption:Key must be supplied by an environment variable or secret provider and contain exactly 32 bytes.");

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

                options.AddPolicy("FinanceManage", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.FinanceManage));
                options.AddPolicy("OrderFulfillment", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.OrderFulfillment));
                options.AddPolicy("KycReview", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.KycReview));
                options.AddPolicy("SecurityDiagnostics", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.SecurityDiagnostics));
                options.AddPolicy("SettingsManage", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.SettingsManage));
                options.AddPolicy("UserManage", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.UserManage));
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
            builder.Services.Configure<SeqOptions>(builder.Configuration.GetSection("Seq"));
            builder.Services.AddHostedService<SeqConnectivityProbe>();

            var app = builder.Build();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseVitorizeRequestLogging();

            app.SeedVitorizeInitialDataAsync();

            // Global Exception Handler
            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = SecurityHeaderPolicy.ContentTypeOptions;
                context.Response.Headers["X-Frame-Options"] = SecurityHeaderPolicy.ApiFrameOptions;
                context.Response.Headers["Referrer-Policy"] = SecurityHeaderPolicy.ReferrerPolicy;
                context.Response.Headers["Permissions-Policy"] = SecurityHeaderPolicy.PermissionsPolicy;
                context.Response.Headers["Content-Security-Policy"] = SecurityHeaderPolicy.ApiContentSecurityPolicy;
                await next();
            });

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

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/uploads/verifications"))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next();
            });

            app.UseStaticFiles();

            // سرو مطمئن فایل‌های آپلودشده (تصاویر محصولات، دسته‌بندی‌ها، بنرها، مدارک)
            // مستقل از وجود پوشه wwwroot در زمان شروع برنامه.
            var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsRoot);
            app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsRoot),
                RequestPath = "/uploads",
                OnPrepareResponse = ctx =>
                {
                    // اجازه‌ی نمایش تصاویر در فروشگاه روی دامنه/پورت دیگر
                    ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                }
            });

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseRateLimiter();

            app.MapControllers();

            var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
            var seqState = SerilogHostConfiguration.SeqState(app.Configuration);
            if (seqState == "InvalidConfiguration")
                startupLogger.LogWarning("Seq was requested but its URL is invalid; console and file sinks remain active. EventType={EventType}", "SeqConfigurationInvalid");
            else if (seqState == "Disabled")
                startupLogger.LogWarning("Seq is disabled; console and file sinks remain active. EventType={EventType}", "SeqDisabled");

            app.Lifetime.ApplicationStarted.Register(() => startupLogger.LogInformation(
                "Vitorize API started in {Environment}. EventType={EventType}", app.Environment.EnvironmentName, OperationalEventNames.ApplicationStarted));
            app.Lifetime.ApplicationStopping.Register(() => startupLogger.LogInformation(
                "Vitorize API is stopping. EventType={EventType}", OperationalEventNames.ApplicationStopping));

            app.Run();
            }
            catch (Exception exception)
            {
                Log.Fatal(
                    "Vitorize API terminated during startup. ExceptionType={ExceptionType} SafeException={SafeException} ExceptionStack={ExceptionStack} EventType={EventType}",
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
        }
    }
}
