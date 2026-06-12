namespace Vitorize.Application.Interfaces
{
    public interface IOutboxService
    {
        Task AddAsync(
            string messageType,
            string payload,
            Guid? aggregateId = null,
            string? aggregateType = null);
    }
}