namespace Vitorize.Application.Interfaces
{
    public interface IIdempotencyService
    {
        Task StartAsync(Guid? userId, string key, string requestHash);

        Task CompleteAsync(
            string key,
            string? responseJson = null,
            int? statusCode = null);

        Task FailAsync(
            string key,
            string? errorMessage = null);
    }
}