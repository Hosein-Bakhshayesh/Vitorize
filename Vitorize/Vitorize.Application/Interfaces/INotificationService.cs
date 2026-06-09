using Vitorize.Application.DTOs.Notifications;

namespace Vitorize.Application.Interfaces
{
    public interface INotificationService
    {
        Task CreateAsync(
            Guid userId,
            byte type,
            string title,
            string message);

        Task<List<NotificationDto>> GetMyNotificationsAsync(
            Guid userId);

        Task MarkAsReadAsync(
            Guid userId,
            Guid notificationId);

        Task MarkAllAsReadAsync(
            Guid userId);
    }
}