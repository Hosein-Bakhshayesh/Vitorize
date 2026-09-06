using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    /// <summary>
    /// FIX-15 admin group announcements. Delivery reuses the existing per-user Notification model;
    /// this service owns audience resolution, the recipient cap, atomicity and audit.
    /// </summary>
    public class AdminNotificationBroadcastService : IAdminNotificationBroadcastService
    {
        private const int MaximumTitleLength = 250;

        private readonly VitorizeDbContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUser;

        public AdminNotificationBroadcastService(
            VitorizeDbContext dbContext,
            INotificationService notificationService,
            IAuditService auditService,
            ICurrentUserService currentUser)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _auditService = auditService;
            _currentUser = currentUser;
        }

        public async Task<BroadcastPreviewResultDto> PreviewAsync(
            BroadcastPreviewRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var audience = ParseAudience(request.Audience);
            var (recipients, ineligible) = await ResolveRecipientsAsync(
                audience, request.SelectedCustomerIds, throwOnIneligible: false, cancellationToken);

            return new BroadcastPreviewResultDto
            {
                RecipientCount = recipients.Count,
                IneligibleCount = ineligible,
                MaximumRecipients = BroadcastRecipientRules.MaximumRecipients,
                ExceedsLimit = recipients.Count > BroadcastRecipientRules.MaximumRecipients
            };
        }

        public async Task<BroadcastDto> SendAsync(
            Guid actorUserId,
            SendBroadcastRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var audience = ParseAudience(request.Audience);
            var title = NormalizeTitle(request.Title);
            var message = NormalizeMessage(request.Message);
            var actionUrl = NotificationActionUrlRules.NormalizeInternalPath(request.ActionUrl);

            // Recipients are re-resolved at send time: account state may have changed since preview,
            // and the preview count is never trusted.
            var (recipients, _) = await ResolveRecipientsAsync(
                audience, request.SelectedCustomerIds, throwOnIneligible: true, cancellationToken);

            if (recipients.Count == 0)
                throw new BusinessException("هیچ گیرنده واجد شرایطی برای این ارسال یافت نشد.");

            if (recipients.Count > BroadcastRecipientRules.MaximumRecipients)
                throw new BusinessException(
                    $"تعداد گیرندگان این ارسال بیشتر از سقف مجاز {BroadcastRecipientRules.MaximumRecipients} کاربر است.");

            var now = DateTime.UtcNow;
            var broadcast = new NotificationBroadcast
            {
                Id = Guid.NewGuid(),
                Title = title,
                Message = message,
                AudienceType = (byte)audience,
                RecipientCount = 0,
                Status = (byte)BroadcastStatus.Sending,
                ActionUrl = actionUrl,
                CreatedByUserId = actorUserId,
                CreatedAt = now
            };

            // Persist the header before delivery. NotificationService commits a bounded batch at a
            // time; holding 5,000 notification/outbox rows in a single transaction would retain
            // SQL Server's transaction log until the entire broadcast completed.
            await _dbContext.NotificationBroadcasts.AddAsync(broadcast, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var delivered = await _notificationService.CreateBulkAsync(
                    broadcast.Id, recipients, title, message, request.SendSms, actorUserId, cancellationToken);

                if (delivered != recipients.Count)
                    throw new BusinessException("ارسال گروهی کامل نشد؛ عملیات لغو شد.");

                // The final state and its one audit record are still committed together. Individual
                // recipient batches were already committed independently to bound transaction-log
                // use and lock duration.
                await using var finalization = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                    : null;
                broadcast.RecipientCount = delivered;
                broadcast.Status = (byte)BroadcastStatus.Sent;
                broadcast.SentAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Exactly one high-level audit record; no recipient list, no page/message body dump.
                await _auditService.LogAsync(
                    actorUserId,
                    "NotificationBroadcastSent",
                    nameof(NotificationBroadcast),
                    broadcast.Id.ToString(),
                    $"audience={audience}; recipients={delivered}; sms={request.SendSms}; title={title}" +
                    (actionUrl is null ? string.Empty : $"; actionUrl={actionUrl}"),
                    _currentUser.IpAddress,
                    _currentUser.UserAgent);

                if (finalization is not null)
                    await finalization.CommitAsync(cancellationToken);
            }
            catch
            {
                // Earlier batches may already be durable. Reflect their real count and make the
                // incomplete broadcast visible to administrators instead of leaving it "Sending".
                _dbContext.ChangeTracker.Clear();
                var delivered = await _dbContext.Notifications
                    .AsNoTracking()
                    .CountAsync(x => x.BroadcastId == broadcast.Id, cancellationToken);
                await _dbContext.NotificationBroadcasts
                    .Where(x => x.Id == broadcast.Id)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.RecipientCount, delivered)
                        .SetProperty(x => x.Status, (byte)BroadcastStatus.Failed), cancellationToken);
                throw;
            }

            return await GetByIdAsync(broadcast.Id, cancellationToken);
        }

        public async Task<PagedResult<BroadcastDto>> GetHistoryAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var query = _dbContext.NotificationBroadcasts.AsNoTracking();
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new BroadcastDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Message = x.Message,
                    AudienceType = x.AudienceType,
                    RecipientCount = x.RecipientCount,
                    Status = x.Status,
                    ActionUrl = x.ActionUrl,
                    CreatedByUserId = x.CreatedByUserId,
                    CreatedByFullName = x.CreatedByUser.FullName,
                    CreatedAt = x.CreatedAt,
                    SentAt = x.SentAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<BroadcastDto>
            {
                Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount
            };
        }

        public async Task<BroadcastDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Inline projection so EF joins the sender name in SQL; a helper method here would be
            // client-evaluated with the navigation left unloaded.
            var broadcast = await _dbContext.NotificationBroadcasts.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new BroadcastDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Message = x.Message,
                    AudienceType = x.AudienceType,
                    RecipientCount = x.RecipientCount,
                    Status = x.Status,
                    ActionUrl = x.ActionUrl,
                    CreatedByUserId = x.CreatedByUserId,
                    CreatedByFullName = x.CreatedByUser.FullName,
                    CreatedAt = x.CreatedAt,
                    SentAt = x.SentAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            return broadcast ?? throw new NotFoundException("ارسال گروهی یافت نشد.");
        }

        /// <summary>
        /// The single recipient resolver shared by Preview and Send, so the two can never drift.
        /// </summary>
        private async Task<(List<Guid> Recipients, int IneligibleCount)> ResolveRecipientsAsync(
            BroadcastAudience audience,
            IReadOnlyCollection<Guid>? selectedIds,
            bool throwOnIneligible,
            CancellationToken cancellationToken)
        {
            var eligible = _dbContext.Users.AsNoTracking().Where(BroadcastRecipientRules.IsEligibleCustomer);

            if (audience == BroadcastAudience.AllCustomers)
                return (await eligible.Select(x => x.Id).ToListAsync(cancellationToken), 0);

            var requested = (selectedIds ?? Array.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (requested.Count == 0)
                throw new BusinessException("حداقل یک مشتری باید انتخاب شود.");

            if (requested.Count > BroadcastRecipientRules.MaximumRecipients)
                throw new BusinessException(
                    $"تعداد گیرندگان این ارسال بیشتر از سقف مجاز {BroadcastRecipientRules.MaximumRecipients} کاربر است.");

            var resolved = await eligible
                .Where(x => requested.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var ineligibleCount = requested.Count - resolved.Count;

            // Never silently downgrade the selection: an admin who picked staff or a blocked
            // account is told, rather than having those recipients quietly dropped.
            if (ineligibleCount > 0 && throwOnIneligible)
                throw new BusinessException(
                    $"{ineligibleCount} کاربر انتخاب‌شده واجد شرایط دریافت اعلان نیستند. انتخاب را اصلاح کنید.");

            return (resolved, ineligibleCount);
        }

        private static BroadcastAudience ParseAudience(byte value) =>
            Enum.IsDefined(typeof(BroadcastAudience), value)
                ? (BroadcastAudience)value
                : throw new BusinessException("مخاطب ارسال معتبر نیست.");

        private static string NormalizeTitle(string? value)
        {
            var title = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                throw new BusinessException("عنوان اعلان الزامی است.");
            if (title.Length > MaximumTitleLength)
                throw new BusinessException($"عنوان اعلان نمی‌تواند بیشتر از {MaximumTitleLength} نویسه باشد.");
            return title;
        }

        private static string NormalizeMessage(string? value)
        {
            var message = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
                throw new BusinessException("متن اعلان الزامی است.");
            return message;
        }

    }
}
