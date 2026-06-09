using Vitorize.Application.DTOs.Tickets;

namespace Vitorize.Application.Interfaces
{
    public interface ITicketService
    {
        Task<List<TicketDto>> GetMyTicketsAsync(Guid userId);

        Task<TicketDto> GetMyTicketByIdAsync(Guid userId, Guid ticketId);

        Task<TicketDto> CreateAsync(Guid userId, CreateTicketRequestDto request);

        Task<TicketDto> AddMessageAsync(
            Guid userId,
            Guid ticketId,
            AddTicketMessageRequestDto request);

        Task<List<TicketDto>> GetAllAsync();

        Task<TicketDto> GetByIdAsync(Guid ticketId);

        Task<TicketDto> AdminAddMessageAsync(
            Guid adminUserId,
            Guid ticketId,
            AdminAddTicketMessageRequestDto request);

        Task<TicketDto> CloseAsync(Guid ticketId);

        Task<TicketDto> ReopenAsync(Guid ticketId);
    }
}