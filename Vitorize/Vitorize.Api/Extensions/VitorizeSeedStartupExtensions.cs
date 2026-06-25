using Vitorize.Application.Interfaces;

namespace Vitorize.Api.Extensions
{
    public static class VitorizeSeedStartupExtensions
    {
        public static void SeedVitorizeInitialDataAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IVitorizeSeedService>();
            seeder.SeedAsync();
        }
    }
}
