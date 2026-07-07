using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Tickets;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class TicketService : ITicketService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly INotificationService _notificationService;

        public TicketService(
            VitorizeDbContext dbContext,
            INotificationService notificationService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
        }

        public async Task<List<TicketDto>> GetMyTicketsAsync(Guid userId)
        {
            return await _dbContext.Tickets
                .Include(x => x.TicketMessages)
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapTicket(x, false))
                .ToListAsync();
        }

        public async Task<TicketDto> GetMyTicketByIdAsync(
            Guid userId,
            Guid ticketId)
        {
            var ticket = await _dbContext.Tickets
                .Include(x => x.TicketMessages)
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == ticketId &&
                    x.UserId == userId);

            if (ticket == null)
                throw new NotFoundException("تیکت یافت نشد.");

            return MapTicket(ticket, false);
        }

        public async Task<TicketDto> CreateAsync(
            Guid userId,
            CreateTicketRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            ValidateCreateRequest(request);

            var now = DateTime.UtcNow;

            if (request.OrderId.HasValue)
            {
                var orderExists = await _dbContext.Orders
                    .AnyAsync(x =>
                        x.Id == request.OrderId.Value &&
                        x.UserId == userId);

                if (!orderExists)
                    throw new BusinessException("سفارش انتخاب شده معتبر نیست.");
            }

            OrderItem? orderItem = null;

            if (request.OrderItemId.HasValue)
            {
                orderItem = await _dbContext.OrderItems
                    .Include(x => x.Order)
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.OrderItemId.Value &&
                        x.Order.UserId == userId);

                if (orderItem == null)
                    throw new BusinessException("آیتم سفارش انتخاب شده معتبر نیست.");

                if (request.OrderId.HasValue &&
                    orderItem.OrderId != request.OrderId.Value)
                    throw new BusinessException("آیتم سفارش با سفارش انتخاب شده همخوانی ندارد.");
            }

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = request.OrderId,
                Subject = request.Subject.Trim(),
                Department = request.Department,
                Priority = request.Priority,
                Status = (byte)TicketStatus.WaitingForAdmin,
                CreatedAt = now,
                UpdatedAt = now
            };

            var firstMessage = new TicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                SenderUserId = userId,
                Message = request.Message.Trim(),
                AttachmentPath = request.AttachmentPath?.Trim(),
                IsInternalNote = false,
                CreatedAt = now
            };

            ticket.TicketMessages.Add(firstMessage);

            await _dbContext.Tickets.AddAsync(ticket);

            if (orderItem != null)
                orderItem.SupportTicketId = ticket.Id;

            await _notificationService.CreateAsync(
                userId,
                (byte)NotificationType.TicketCreated,
                "تیکت ثبت شد",
                $"تیکت «{ticket.Subject}» با موفقیت ثبت شد و در انتظار پاسخ پشتیبانی است.");

            await _dbContext.SaveChangesAsync();

            return MapTicket(ticket, false);
        }

        public async Task<TicketDto> AddMessageAsync(
            Guid userId,
            Guid ticketId,
            AddTicketMessageRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                throw new BusinessException("متن پیام الزامی است.");

            var ticket = await _dbContext.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == ticketId &&
                    x.UserId == userId);

            if (ticket == null)
                throw new NotFoundException("تیکت یافت نشد.");

            if (ticket.Status == (byte)TicketStatus.Closed)
                throw new BusinessException("این تیکت بسته شده است.");

            var now = DateTime.UtcNow;

            // Insert the new message on its own (same shape as ticket creation) and update the
            // ticket header with a set-based statement. Doing the header change as a tracked
            // UPDATE inside this SaveChanges tripped an EF optimistic-concurrency check
            // ("0 rows affected") against the DB-first schema, so it is kept separate.
            _dbContext.TicketMessages.Add(new TicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                SenderUserId = userId,
                Message = request.Message.Trim(),
                AttachmentPath = request.AttachmentPath?.Trim(),
                IsInternalNote = false,
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();

            await _dbContext.Tickets
                .Where(x => x.Id == ticketId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, (byte)TicketStatus.WaitingForAdmin)
                    .SetProperty(t => t.UpdatedAt, now));

            return await GetByIdAsync(ticketId);
        }

        public async Task<List<TicketDto>> GetAllAsync()
        {
            return await _dbContext.Tickets
                .Include(x => x.TicketMessages)
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapTicket(x, true))
                .ToListAsync();
        }

        public async Task<TicketDto> GetByIdAsync(Guid ticketId)
        {
            var ticket = await _dbContext.Tickets
                .Include(x => x.TicketMessages)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId);

            if (ticket == null)
                throw new NotFoundException("تیکت یافت نشد.");

            return MapTicket(ticket, true);
        }

        public async Task<TicketDto> AdminAddMessageAsync(
            Guid adminUserId,
            Guid ticketId,
            AdminAddTicketMessageRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                throw new BusinessException("متن پیام الزامی است.");

            var ticket = await _dbContext.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId);

            if (ticket == null)
                throw new NotFoundException("تیکت یافت نشد.");

            if (ticket.Status == (byte)TicketStatus.Closed)
                throw new BusinessException("این تیکت بسته شده است.");

            var now = DateTime.UtcNow;

            _dbContext.TicketMessages.Add(new TicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                SenderUserId = adminUserId,
                Message = request.Message.Trim(),
                AttachmentPath = request.AttachmentPath?.Trim(),
                IsInternalNote = request.IsInternalNote,
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();

            if (!request.IsInternalNote)
            {
                await _notificationService.CreateAsync(
                    ticket.UserId,
                    (byte)NotificationType.TicketReply,
                    "پاسخ پشتیبانی",
                    $"برای تیکت «{ticket.Subject}» پاسخ جدید ثبت شد.");

                await _dbContext.Tickets
                    .Where(x => x.Id == ticketId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.Status, (byte)TicketStatus.WaitingForCustomer)
                        .SetProperty(t => t.UpdatedAt, now));
            }
            else
            {
                await _dbContext.Tickets
                    .Where(x => x.Id == ticketId)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, now));
            }

            return await GetByIdAsync(ticketId);
        }

        public async Task<TicketDto> CloseAsync(Guid ticketId)
        {
            var ticket = await _dbContext.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId);

            if (ticket == null)
                throw new NotFoundException("تیکت یافت نشد.");

            var now = DateTime.UtcNow;

            await _notificationService.CreateAsync(
                ticket.UserId,
                (byte)NotificationType.TicketClosed,
                "تیکت بسته شد",
                $"تیکت «{ticket.Subject}» بسته شد.");

            await _dbContext.Tickets
                .Where(x => x.Id == ticketId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, (byte)TicketStatus.Closed)
                    .SetProperty(t => t.ClosedAt, now)
                    .SetProperty(t => t.UpdatedAt, now));

            return await GetByIdAsync(ticketId);
        }

        public async Task<TicketDto> ReopenAsync(Guid ticketId)
        {
            var ticket = await _dbContext.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId);

            if (ticket == null)
                throw new NotFoundException("تیکت یافت نشد.");

            var now = DateTime.UtcNow;

            await _notificationService.CreateAsync(
                ticket.UserId,
                (byte)NotificationType.TicketReply,
                "تیکت باز شد",
                $"تیکت «{ticket.Subject}» دوباره باز شد.");

            await _dbContext.Tickets
                .Where(x => x.Id == ticketId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, (byte)TicketStatus.WaitingForAdmin)
                    .SetProperty(t => t.ClosedAt, (DateTime?)null)
                    .SetProperty(t => t.UpdatedAt, now));

            return await GetByIdAsync(ticketId);
        }

        private static void ValidateCreateRequest(CreateTicketRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Subject))
                throw new BusinessException("موضوع تیکت الزامی است.");

            if (string.IsNullOrWhiteSpace(request.Message))
                throw new BusinessException("متن پیام الزامی است.");

            if (!Enum.IsDefined(typeof(TicketDepartment), request.Department))
                throw new BusinessException("دپارتمان تیکت معتبر نیست.");

            if (!Enum.IsDefined(typeof(TicketPriority), request.Priority))
                throw new BusinessException("اولویت تیکت معتبر نیست.");
        }

        private static TicketDto MapTicket(Ticket ticket, bool includeInternalNotes)
        {
            return new TicketDto
            {
                Id = ticket.Id,
                UserId = ticket.UserId,
                OrderId = ticket.OrderId,
                Subject = ticket.Subject,
                Department = ticket.Department,
                Priority = ticket.Priority,
                Status = ticket.Status,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt,
                ClosedAt = ticket.ClosedAt,
                Messages = ticket.TicketMessages
                    .Where(x => includeInternalNotes || !x.IsInternalNote)
                    .OrderBy(x => x.CreatedAt)
                    .Select(MapMessage)
                    .ToList()
            };
        }

        private static TicketMessageDto MapMessage(TicketMessage message)
        {
            return new TicketMessageDto
            {
                Id = message.Id,
                TicketId = message.TicketId,
                SenderUserId = message.SenderUserId,
                Message = message.Message,
                AttachmentPath = message.AttachmentPath,
                IsInternalNote = message.IsInternalNote,
                CreatedAt = message.CreatedAt
            };
        }
    }
}