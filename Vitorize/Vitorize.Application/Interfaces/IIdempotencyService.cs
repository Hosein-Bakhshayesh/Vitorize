namespace Vitorize.Application.Interfaces
{
    public interface IIdempotencyService
    {
        Task StartAsync(
            Guid? userId,
            string key,
            string requestHash);

        Task CompleteAsync(
            string key);

        Task FailAsync(
            string key);
    }
}