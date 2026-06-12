using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.DTOs.Outbox;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IOutboxService _outboxService;

        public NotificationService(
            VitorizeDbContext dbContext,
            IOutboxService outboxService)
        {
            _dbContext = dbContext;
            _outboxService = outboxService;
        }

        public async Task CreateAsync(
            Guid userId,
            byte type,
            string title,
            string message)
        {
            if (userId == Guid.Empty)
                return;

            var now = DateTime.UtcNow;

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = now
            };

            await _dbContext.Notifications.AddAsync(notification);

            var payload = JsonSerializer.Serialize(
                new NotificationCreatedEventDto
                {
                    NotificationId = notification.Id,
                    UserId = userId,
                    Type = type,
                    Title = title,
                    Message = message,
                    CreatedAt = now
                });

            await _outboxService.AddAsync(
                messageType: "NotificationCreated",
                payload: payload,
                aggregateId: notification.Id,
                aggregateType: "Notification");

            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<NotificationDto>> GetMyNotificationsAsync(
            Guid userId)
        {
            return await _dbContext.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new NotificationDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Message = x.Message,
                    Type = x.Type,
                    IsRead = x.IsRead,
                    CreatedAt = x.CreatedAt,
                    ReadAt = x.ReadAt
                })
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(
            Guid userId,
            Guid notificationId)
        {
            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.UserId == userId);

            if (notification == null)
                throw new NotFoundException("اعلان یافت نشد.");

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            var notifications = await _dbContext.Notifications
                .Where(x =>
                    x.UserId == userId &&
                    !x.IsRead)
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var item in notifications)
            {
                item.IsRead = true;
                item.ReadAt = now;
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}