namespace Vitorize.Application.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(
            Guid? userId,
            string actionType,
            string entityName,
            string? entityId = null,
            string? data = null,
            string? ipAddress = null,
            string? userAgent = null);
    }
}