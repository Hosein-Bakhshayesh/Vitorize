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

        /// <summary>
        /// ارسال اعلان سیستمی توسط ادمین به یک کاربر مشخص (با اعتبارسنجی وجود کاربر).
        /// </summary>
        Task SendSystemNotificationAsync(
            Guid userId,
            string title,
            string message);

        /// <summary>
        /// FIX-15 bulk delivery for one broadcast. Inserts announcement rows in bounded batches
        /// with a single SaveChanges per batch — deliberately not a loop over
        /// <see cref="CreateAsync"/>, which would issue one SaveChanges and one outbox row per
        /// recipient. Emits no per-recipient outbox message.
        /// </summary>
        /// <returns>The number of notification rows created.</returns>
        Task<int> CreateBulkAsync(
            Guid broadcastId,
            IReadOnlyCollection<Guid> recipientUserIds,
            string title,
            string message,
            CancellationToken cancellationToken = default);

        Task<List<NotificationDto>> GetMyNotificationsAsync(
            Guid userId);

        Task<int> GetUnreadCountAsync(
            Guid userId);

        Task MarkAsReadAsync(
            Guid userId,
            Guid notificationId);

        Task MarkAllAsReadAsync(
            Guid userId);
    }
}