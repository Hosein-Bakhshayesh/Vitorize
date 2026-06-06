namespace Vitorize.Web.Services.Storage
{
    public interface IFileStorageService
    {
        Task<string?> SaveAsync(
            IFormFile? file,
            string folder,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string? relativePath,
            CancellationToken cancellationToken = default);

        bool Exists(string? relativePath);
    }
}