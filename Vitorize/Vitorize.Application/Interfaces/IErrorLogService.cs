namespace Vitorize.Application.Interfaces
{
    public interface IErrorLogService
    {
        Task LogAsync(
            Exception exception);
    }
}