using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Serilog;
using Vitorize.Api.BackgroundServices;
using Vitorize.Api.Extensions;
using Vitorize.Api.Filters;
using Vitorize.Api.Logging;
using Vitorize.Api.Hosting;
using Vitorize.Api.Middlewares;
using Vitorize.Api.Services;
using Vitorize.Application;
using Vitorize.Application.Common;
using Vitorize.Infrastructure;
using Vitorize.Infrastructure.Persistence;
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
            // Local interactive debugging (e.g. the Visual Studio multi-project "New Profile" launch)
            // does not always apply a launch profile, so ASPNETCORE_ENVIRONMENT defaults to Production
            // and the Development-only User Secrets configuration source is skipped - which is why the
            // secret guards below would otherwise fail on a developer machine. When a debugger is
            // attached AND no environment was explicitly chosen, default to Development so the standard,
            // correctly-ordered secret sources load (User Secrets sit just below environment variables).
            // Deployed Production hosts run without a debugger, so this never affects them and the
            // startup validation below remains fully enforced.
            if (Debugger.IsAttached &&
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
            builder.Services.AddSingleton<HostingStoragePaths>();
            var hostingPaths = new HostingStoragePaths(builder.Environment, builder.Configuration);
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(hostingPaths.DataProtectionKeysPath))
                .SetApplicationName("Vitorize");
            builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options => hostingPaths.ConfigureForwardedHeaders(options));

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
            var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (builder.Environment.IsProduction() && corsOrigins.Length == 0)
                throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one origin in Production.");

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("VitorizeCors", policy =>
                {
                    policy
                        .WithOrigins(corsOrigins)
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

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("CHANGE_THIS_BEFORE_PRODUCTION", StringComparison.Ordinal))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing or still contains the production placeholder.");

            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddScoped<Vitorize.Api.Services.CartIdentityResolver>();
            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
                builder.Services.AddSingleton<Vitorize.Api.Services.TestingCartFaultService>();
            builder.Services.AddHostedService<Vitorize.Api.BackgroundServices.GuestCartCleanupService>();
            builder.Services.Configure<Vitorize.Api.BackgroundServices.KycDeadlineProcessingOptions>(
                builder.Configuration.GetSection(Vitorize.Api.BackgroundServices.KycDeadlineProcessingOptions.SectionName));
            builder.Services.AddHostedService<Vitorize.Api.BackgroundServices.KycDeadlineExpiryBackgroundService>();
            builder.Services.AddScoped<IReadinessProbe, SqlServerReadinessProbe>();

            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("Jwt"));

            var jwtSettings = builder.Configuration
                .GetSection("Jwt")
                .Get<JwtSettings>();

            if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.SecretKey) ||
                Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32 ||
                jwtSettings.SecretKey.Contains("CHANGE_THIS_BEFORE_PRODUCTION", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Jwt:SecretKey is missing or must contain at least 32 bytes.");
            }

            var encryptionKey = builder.Configuration["Encryption:Key"];
            if (string.IsNullOrWhiteSpace(encryptionKey) || Encoding.UTF8.GetByteCount(encryptionKey) != 32 ||
                encryptionKey.Contains("CHANGE_THIS_BEFORE_PRODUCTION", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Encryption:Key is missing or must contain exactly 32 bytes.");

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
                options.AddPolicy("KycManage", policy => policy.RequireClaim(
                    Vitorize.Application.Common.AdminPermissions.ClaimType,
                    Vitorize.Application.Common.AdminPermissions.KycManage));
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
            var testingRateLimit = builder.Environment.IsEnvironment("Testing") ? 1_000 : (int?)null;
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddFixedWindowLimiter("login", opt =>
                {
                    opt.PermitLimit = testingRateLimit ?? 5;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueProcessingOrder =
                        System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                    opt.QueueLimit = 0;
                });

                options.AddFixedWindowLimiter("otp", opt =>
                {
                    opt.PermitLimit = testingRateLimit ?? 3;
                    opt.Window = TimeSpan.FromMinutes(1);
                });

                options.AddFixedWindowLimiter("register", opt =>
                {
                    opt.PermitLimit = testingRateLimit ?? 3;
                    opt.Window = TimeSpan.FromMinutes(5);
                });
            });

            // Background Services
            builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
            builder.Services.AddHostedService<BackgroundJobProcessor>();
            builder.Services.Configure<SeqOptions>(builder.Configuration.GetSection("Seq"));
            builder.Services.AddHostedService<SeqConnectivityProbe>();

            var app = builder.Build();
            app.Services.GetRequiredService<HostingStoragePaths>().ValidateAndPrepare();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseVitorizeRequestLogging();

            app.SeedVitorizeInitialDataAsync();
            app.ValidateProductionPaymentConfiguration();

            // Global Exception Handler
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseForwardedHeaders();

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
            var uploadsRoot = app.Services.GetRequiredService<HostingStoragePaths>().PublicMediaRoot;
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

            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                app.MapPost("/api/testing/cart/fail-next-read", (TestingCartFaultService faults) =>
                {
                    faults.FailNextCartRead();
                    return Results.NoContent();
                });

            }

            if (app.Environment.IsEnvironment("Testing") &&
                app.Configuration.GetValue<bool>("Testing:UseFakeSms"))
            {
                app.MapPost("/api/testing/payment-fault", (string? mode,
                    Vitorize.Infrastructure.Services.Testing.TestingPaymentFaultService faults) =>
                {
                    faults.Set(mode);
                    return Results.NoContent();
                });

                app.MapGet("/api/testing/sms/latest-otp", (
                    string mobile,
                    Vitorize.Infrastructure.Services.Sms.TestingSmsSender sender) =>
                    sender.TryGetLatestOtp(mobile, out var code, out var expire)
                        ? Results.Ok(new { code, expire })
                        : Results.NotFound());

                app.MapPost("/api/testing/otp/expire", async (
                    string mobile,
                    VitorizeDbContext db,
                    CancellationToken cancellationToken) =>
                {
                    if (!IranMobile.TryNormalize(mobile, out var normalized))
                        return Results.BadRequest();

                    var affected = await db.OtpCodes
                        .Where(x => x.Mobile == normalized && x.ConsumedAt == null)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)), cancellationToken);
                    return Results.Ok(new { affected });
                });

                // Safe, non-sensitive aggregate state for an order, used by the support/ticket delivery
                // E2E to assert database invariants (no codes, message text or PII are ever returned).
                app.MapGet("/api/testing/support-state", async (
                    Guid orderId,
                    VitorizeDbContext db,
                    CancellationToken cancellationToken) =>
                {
                    var order = await db.Orders.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
                    if (order is null) return Results.NotFound();

                    var itemIds = await db.OrderItems.Where(x => x.OrderId == orderId)
                        .Select(x => x.Id).ToListAsync(cancellationToken);

                    return Results.Ok(new
                    {
                        orderUserId = order.UserId,
                        paid = order.PaymentStatus == (byte)Vitorize.Shared.Enums.PaymentStatus.Paid,
                        orderStatus = order.Status,
                        orderItems = itemIds.Count,
                        supportItems = await db.OrderItems.CountAsync(x =>
                            x.OrderId == orderId && x.DeliveryType == (byte)Vitorize.Shared.Enums.DeliveryType.SupportRequired, cancellationToken),
                        giftCodesAssigned = await db.GiftCodes.CountAsync(x =>
                            x.OrderItemId != null && itemIds.Contains(x.OrderItemId.Value), cancellationToken),
                        instantDeliveries = await db.OrderItemDeliveries.CountAsync(x =>
                            itemIds.Contains(x.OrderItemId) && x.DeliveryType == (byte)Vitorize.Shared.Enums.DeliveryType.Instant, cancellationToken),
                        manualDeliveries = await db.OrderItemDeliveries.CountAsync(x =>
                            itemIds.Contains(x.OrderItemId) &&
                            (x.DeliveryType == (byte)Vitorize.Shared.Enums.DeliveryType.Manual ||
                             x.DeliveryType == (byte)Vitorize.Shared.Enums.DeliveryType.SupportRequired), cancellationToken),
                        tickets = await db.Tickets.CountAsync(x => x.OrderId == orderId, cancellationToken),
                        fulfillmentTickets = await db.Tickets.CountAsync(x =>
                            x.OrderId == orderId && x.IsFulfillmentTicket, cancellationToken),
                        fulfillmentItemLinks = await db.OrderItems.CountAsync(x =>
                            x.OrderId == orderId && x.SupportTicketId != null, cancellationToken),
                        paymentId = await db.Payments.Where(x => x.OrderId == orderId)
                            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken),
                        ticketUserId = await db.Tickets.Where(x => x.OrderId == orderId)
                            .Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(cancellationToken)
                    });
                });

                // Isolated-browser-test-only payment attempt projection. Authorities are exposed
                // solely to let the fake gateway cancellation/late-callback scenarios exercise the
                // real callback endpoint; this route is never mapped outside Testing.
                app.MapGet("/api/testing/payment-state", async (
                    Guid orderId,
                    VitorizeDbContext db,
                    CancellationToken cancellationToken) =>
                {
                    var attempts = await db.Payments.AsNoTracking()
                        .Where(x => x.OrderId == orderId)
                        .OrderBy(x => x.RequestedAt)
                        .Select(x => new { x.Id, x.Gateway, x.Authority, x.Status, x.ProviderStatusCode })
                        .ToListAsync(cancellationToken);
                    return attempts.Count == 0 ? Results.NotFound() : Results.Ok(new { attempts });
                });

                // Testing-only catalog projection used by browser tests to verify that Admin UI
                // operations reached the relational model. It intentionally exposes no customer,
                // order, gift-code value, or other production-sensitive data and is never mapped
                // outside the isolated Testing environment.
                app.MapGet("/api/testing/product-state", async (
                    string slug,
                    VitorizeDbContext db,
                    CancellationToken cancellationToken) =>
                {
                    var normalizedSlug = (slug ?? string.Empty).Trim().ToLowerInvariant();
                    var product = await db.Products.AsNoTracking()
                        .Where(x => x.Slug == normalizedSlug && !x.IsDeleted)
                        .Select(x => new
                        {
                            x.Id,
                            x.CategoryId,
                            x.BrandId,
                            x.Title,
                            x.Slug,
                            x.ShortDescription,
                            x.FullDescription,
                            x.ProductType,
                            x.DeliveryType,
                            x.BasePrice,
                            x.DiscountPrice,
                            x.CurrencyType,
                            x.MinOrderQuantity,
                            x.MaxOrderQuantity,
                            x.IsFeatured,
                            x.IsActive,
                            x.SeoTitle,
                            x.SeoDescription,
                            x.FocusKeyword,
                            x.ThumbnailImagePath,
                            x.ThumbnailAltText,
                            Tags = x.Tags.OrderBy(t => t.Title).Select(t => new { t.Id, t.Title }).ToList(),
                            Variants = x.ProductVariants.OrderBy(v => v.SortOrder).ThenBy(v => v.Title).Select(v => new
                            {
                                v.Id, v.Title, v.Sku, v.Price, v.DiscountPrice, v.Value,
                                v.StockMode, v.IsDefault, v.IsActive, v.SortOrder
                            }).ToList(),
                            Images = x.ProductImages.OrderBy(i => i.SortOrder).Select(i => new
                            {
                                i.Id, i.ImagePath, i.AltText, i.SortOrder
                            }).ToList(),
                            Features = x.ProductFeatures.OrderBy(f => f.SortOrder).Select(f => new
                            {
                                f.Id, f.Title, f.Value, f.IconKey, f.IsActive, f.SortOrder
                            }).ToList(),
                            InputFields = x.ProductInputFields.OrderBy(f => f.SortOrder).Select(f => new
                            {
                                f.Id, f.Key, f.Label, f.FieldType, f.IsRequired, f.DisplayStage,
                                f.IsActive, f.SortOrder
                            }).ToList()
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (product is null) return Results.NotFound();

                    var duplicateSkus = await db.ProductVariants.AsNoTracking()
                        .Where(x => x.Sku != null && x.Sku != string.Empty)
                        .GroupBy(x => x.Sku)
                        .CountAsync(x => x.Count() > 1, cancellationToken);
                    var productsWithMultipleDefaults = await db.ProductVariants.AsNoTracking()
                        .Where(x => x.IsDefault)
                        .GroupBy(x => x.ProductId)
                        .CountAsync(x => x.Count() > 1, cancellationToken);

                    return Results.Ok(new
                    {
                        product,
                        integrity = new
                        {
                            duplicateSkus,
                            productsWithMultipleDefaults,
                            invalidProductPricing = await db.Products.CountAsync(x => !x.IsDeleted &&
                                (x.BasePrice < 0 || x.DiscountPrice < 0 || x.DiscountPrice > x.BasePrice), cancellationToken),
                            invalidVariantPricing = await db.ProductVariants.CountAsync(x =>
                                x.Price < 0 || x.DiscountPrice < 0 || x.DiscountPrice > x.Price, cancellationToken),
                            orphanVariants = await db.ProductVariants.CountAsync(x =>
                                !db.Products.Any(p => p.Id == x.ProductId), cancellationToken),
                            orphanImages = await db.ProductImages.CountAsync(x =>
                                !db.Products.Any(p => p.Id == x.ProductId), cancellationToken),
                            orphanFeatures = await db.ProductFeatures.CountAsync(x =>
                                !db.Products.Any(p => p.Id == x.ProductId), cancellationToken),
                            orphanInputFields = await db.ProductInputFields.CountAsync(x =>
                                !db.Products.Any(p => p.Id == x.ProductId), cancellationToken)
                        }
                    });
                });
            }

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
