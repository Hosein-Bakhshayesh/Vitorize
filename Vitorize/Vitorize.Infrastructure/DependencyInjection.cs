using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Interceptors;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<VitorizeDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

                options.AddInterceptors(
                    serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
            });

            // Authentication
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IAdminCategoryService, AdminCategoryService>();
            services.AddScoped<IAdminBrandService, AdminBrandService>();
            services.AddScoped<IAdminProductService, AdminProductService>();
            services.AddScoped<IAdminProductTagService, AdminProductTagService>();
            services.AddScoped<IAdminProductVariantService, AdminProductVariantService>();
            services.AddScoped<IEncryptionService, AesEncryptionService>();
            services.AddSingleton<IHtmlContentSanitizer, StrictHtmlContentSanitizer>();
            services.AddScoped<IAdminGiftCodeService, AdminGiftCodeService>();
            services.AddScoped<IGiftCodeReservationService, GiftCodeReservationService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<ICheckoutService, CheckoutService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IGiftCodeDeliveryService, GiftCodeDeliveryService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IAdminCouponService, AdminCouponService>();
            services.AddScoped<ICouponService, CouponService>();
            services.AddScoped<IAdminDashboardService, AdminDashboardService>();
            services.AddScoped<IAdminProductImageService, AdminProductImageService>();
            services.AddScoped<IWalletService, WalletService>();
            services.AddScoped<IWalletTopUpService, WalletTopUpService>();
            services.AddScoped<IWishlistService, WishlistService>();
            services.AddScoped<IVerificationService, VerificationService>();
            services.AddScoped<ITicketService, TicketService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<ISecurityLogService, SecurityLogService>();
            services.AddScoped<IErrorLogService, ErrorLogService>();
            services.AddScoped<IAdminUserService, AdminUserService>();
            services.AddScoped<IIdempotencyService, IdempotencyService>();
            services.AddScoped<IOutboxService, OutboxService>();
            services.AddScoped<ISettingService, SettingService>();

            // Testing-environment-only fault injection (Off by default; guarded by IHostEnvironment).
            services.Configure<Services.Testing.TestingFaultInjectionOptions>(
                configuration.GetSection(Services.Testing.TestingFaultInjectionOptions.SectionName));

            // ───────────── SMS (SMS.ir) ─────────────
            // Sender و SettingsProvider به‌صورت Singleton ثبت می‌شوند تا HttpClient داخلی SDK
            // و کش تنظیمات میان درخواست‌ها بازاستفاده شود. SmsService و Enqueuer در سطح درخواست هستند.
            if (configuration.GetValue<bool>("Testing:UseFakeSms"))
            {
                services.AddSingleton<TestingSmsSender>();
                services.AddSingleton<ISmsSender>(provider =>
                    provider.GetRequiredService<TestingSmsSender>());
            }
            else
            {
                services.AddSingleton<ISmsSender, SmsIrSender>();
            }
            services.AddSingleton<ISmsSettingsProvider, SmsSettingsProvider>();
            services.AddScoped<ISmsService, SmsService>();
            services.AddScoped<ISmsOutboxEnqueuer, SmsOutboxEnqueuer>();
            services.AddScoped<ISmsHistoryService, SmsHistoryService>();
            services.AddScoped<IAdminSmsManagementService, AdminSmsManagementService>();
            services.AddScoped<IAdminReportService, AdminReportService>();
            services.AddScoped<IStorefrontService, StorefrontService>();
            services.AddScoped<ISeoService, SeoService>();
            services.AddScoped<IAdminBannerService, AdminBannerService>();
            services.AddScoped<IProductReviewService, ProductReviewService>();
            services.AddScoped<IAdminProductReviewService, AdminProductReviewService>();
            services.AddScoped<AuditSaveChangesInterceptor>();

            services.TryAddScoped<IAdminSystemReadService, AdminSystemReadService>();
            services.TryAddScoped<IAdminMonitoringService, AdminMonitoringService>();
            services.TryAddSingleton<IWorkerHeartbeatRegistry, WorkerHeartbeatRegistry>();
            services.TryAddScoped<IAdminPaymentReadService, AdminPaymentReadService>();
            services.TryAddScoped<IAdminRoleReadService, AdminRoleReadService>();
            services.TryAddScoped<IAdminNotificationReadService, AdminNotificationReadService>();
            services.TryAddScoped<IAdminWalletReadService, AdminWalletReadService>();
            services.TryAddScoped<IVitorizeSeedService, VitorizeSeedService>();


            services.AddHttpClient<IZarinpalGatewayService, ZarinpalGatewayService>();

            services.Configure<EncryptionSettings>(
                configuration.GetSection("Encryption"));

            services.Configure<BootstrapAdminOptions>(
                configuration.GetSection(BootstrapAdminOptions.SectionName));

            services.Configure<DevelopmentDemoUserOptions>(
                configuration.GetSection(DevelopmentDemoUserOptions.SectionName));

            services.Configure<ZarinpalSettings>(
                configuration.GetSection("Zarinpal"));
            services.Configure<MonitoringOptions>(
                configuration.GetSection("Monitoring"));


            // Framework Services
            services.AddHttpContextAccessor();

            return services;
        }
    }
}
