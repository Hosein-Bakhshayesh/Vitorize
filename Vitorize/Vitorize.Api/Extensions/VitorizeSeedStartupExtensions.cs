using Vitorize.Application.Interfaces;

namespace Vitorize.Api.Extensions
{
    public static class VitorizeSeedStartupExtensions
    {
        public static void SeedVitorizeInitialDataAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IVitorizeSeedService>();

            // اجرای blocking در Startup؛ نسخه‌ی قبلی fire-and-forget بود و scope پیش از
            // پایان Seed آزاد می‌شد (خطای «connection to database ''» در شروع برنامه).
            seeder.SeedAsync().GetAwaiter().GetResult();
        }
    }
}
