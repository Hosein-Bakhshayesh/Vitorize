using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;

namespace Vitorize.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<VitorizeDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });

            // Authentication
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IAdminCategoryService, AdminCategoryService>();
            services.AddScoped<IAdminBrandService, AdminBrandService>();
            services.AddScoped<IAdminProductService, AdminProductService>();
            services.AddScoped<IAdminProductVariantService, AdminProductVariantService>();
            services.AddScoped<IEncryptionService, AesEncryptionService>();
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

            services.Configure<EncryptionSettings>(
                configuration.GetSection("Encryption"));

            // Framework Services
            services.AddHttpContextAccessor();

            return services;
        }
    }
}