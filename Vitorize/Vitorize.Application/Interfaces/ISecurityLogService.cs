namespace Vitorize.Application.Interfaces
{
    public interface ISecurityLogService
    {
        Task LogAsync(
            Guid? userId,
            string eventType,
            bool isSuccessful,
            string? description = null,
            string? ipAddress = null,
            string? userAgent = null);
    }
}