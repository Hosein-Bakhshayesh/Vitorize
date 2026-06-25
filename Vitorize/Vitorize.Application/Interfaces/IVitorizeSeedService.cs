namespace Vitorize.Application.Interfaces
{
    public interface IVitorizeSeedService
    {
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
