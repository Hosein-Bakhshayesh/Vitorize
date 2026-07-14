using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services.Sms
{
    public sealed class AdminSmsManagementService : IAdminSmsManagementService
    {
        private static readonly Regex SafeReference = new(
            "^[A-Za-z0-9][A-Za-z0-9._-]{0,149}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly VitorizeDbContext _db;
        private readonly ISmsService _sms;
        private readonly ISmsSettingsProvider _smsSettings;
        private readonly ISmsOutboxEnqueuer _outbox;
        private readonly ISmsHistoryService _history;
        private readonly ISecurityLogService _securityLog;

        public AdminSmsManagementService(
            VitorizeDbContext db,
            ISmsService sms,
            ISmsSettingsProvider smsSettings,
            ISmsOutboxEnqueuer outbox,
            ISmsHistoryService history,
            ISecurityLogService securityLog)
        {
            _db = db;
            _sms = sms;
            _smsSettings = smsSettings;
            _outbox = outbox;
            _history = history;
            _securityLog = securityLog;
        }

        public async Task<PagedResult<SmsHistoryItemDto>> GetHistoryAsync(
            SmsHistoryFilterDto filter,
            bool allowFullMobile,
            CancellationToken cancellationToken = default)
        {
            var query = ApplyFilter(_db.SmsMessages.AsNoTracking(), filter);
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new SmsHistoryItemDto
                {
                    Id = x.Id,
                    Mobile = allowFullMobile ? x.Mobile : x.MaskedMobile,
                    MaskedMobile = x.MaskedMobile,
                    Purpose = x.Purpose,
                    SendType = x.SendType,
                    TemplateKey = x.TemplateKey,
                    TemplateId = x.TemplateId,
                    PublicReference = x.PublicReference,
                    Provider = x.Provider,
                    Status = x.Status,
                    ProviderMessageId = x.ProviderMessageId,
                    ProviderErrorCode = x.ProviderErrorCode,
                    ProviderErrorMessage = x.ProviderErrorMessage,
                    DeliveryCost = x.DeliveryCost,
                    RetryCount = x.RetryCount,
                    MaxRetryCount = x.MaxRetryCount,
                    CreatedAt = x.CreatedAt,
                    LastAttemptAt = x.LastAttemptAt,
                    SentAt = x.SentAt,
                    FailedAt = x.FailedAt,
                    NextRetryAt = x.NextRetryAt,
                    CreatedByUserId = x.CreatedByUserId,
                    CreatedByName = x.CreatedByUser == null ? null : x.CreatedByUser.FullName,
                    RelatedEntityType = x.RelatedEntityType,
                    RelatedEntityId = x.RelatedEntityId,
                    RelatedEntityReference = x.RelatedEntityReference,
                    SafeMessagePreview = x.SafeMessagePreview,
                    InternalNote = x.InternalNote,
                    CorrelationId = x.CorrelationId
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.StatusName = StatusName(row.Status);
                row.SendTypeName = SendTypeName(row.SendType);
            }

            return new PagedResult<SmsHistoryItemDto>
            {
                Items = rows,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task<SmsHistoryItemDto> GetByIdAsync(
            Guid id,
            bool allowFullMobile,
            CancellationToken cancellationToken = default)
        {
            var message = await _db.SmsMessages
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.Attempts)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("رکورد پیامک یافت نشد.");

            var dto = Map(message, allowFullMobile);
            dto.Attempts = message.Attempts
                .OrderBy(x => x.AttemptNumber)
                .Select(x => new SmsAttemptDto
                {
                    AttemptNumber = x.AttemptNumber,
                    StatusName = StatusName(x.Status),
                    ProviderMessageId = x.ProviderMessageId,
                    ProviderErrorCode = x.ProviderErrorCode,
                    ProviderErrorMessage = x.ProviderErrorMessage,
                    DeliveryCost = x.DeliveryCost,
                    AttemptedAt = x.AttemptedAt,
                    CompletedAt = x.CompletedAt
                })
                .ToList();
            return dto;
        }

        public async Task<SmsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;
            return new SmsSummaryDto
            {
                SentToday = await _db.SmsMessages.CountAsync(x => x.Status == (byte)SmsMessageStatus.Sent && x.SentAt >= today, cancellationToken),
                FailedToday = await _db.SmsMessages.CountAsync(x => (x.Status == (byte)SmsMessageStatus.Failed || x.Status == (byte)SmsMessageStatus.DeadLetter) && x.CreatedAt >= today, cancellationToken),
                PendingOrRetrying = await _db.SmsMessages.CountAsync(x => x.Status == (byte)SmsMessageStatus.Pending || x.Status == (byte)SmsMessageStatus.Processing || x.Status == (byte)SmsMessageStatus.Retrying, cancellationToken),
                OtpMessages = await _db.SmsMessages.CountAsync(x => x.SendType == (byte)SmsSendType.OtpTemplate && x.CreatedAt >= today, cancellationToken),
                NotificationMessages = await _db.SmsMessages.CountAsync(x => x.SendType == (byte)SmsSendType.NotificationTemplate && x.CreatedAt >= today, cancellationToken),
                CustomMessages = await _db.SmsMessages.CountAsync(x => x.SendType == (byte)SmsSendType.CustomText && x.CreatedAt >= today, cancellationToken)
            };
        }

        public async Task<SmsHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            var options = await _smsSettings.GetAsync(cancellationToken);
            var account = await _sms.GetAccountStatusAsync(cancellationToken);
            var (configured, message) = await _sms.ValidateConfigurationAsync(cancellationToken);
            return new SmsHealthDto
            {
                IsEnabled = options.IsEnabled,
                IsConfigured = configured,
                ConnectionOk = account.IsSuccess,
                Credit = account.Credit,
                Lines = account.Lines ?? [],
                OtpTemplateId = options.GetTemplateId(SmsTemplateKeys.GenericOtp),
                NotificationTemplateId = options.GetTemplateId(SmsTemplateKeys.UniversalNotification),
                PendingOutboxCount = await _db.OutboxMessages.CountAsync(x => x.MessageType == OutboxMessageTypes.SmsSend && (x.Status == 0 || x.Status == 1), cancellationToken),
                FailedOutboxCount = await _db.OutboxMessages.CountAsync(x => x.MessageType == OutboxMessageTypes.SmsSend && x.Status == 3, cancellationToken),
                CustomSendEnabled = await GetBoolSettingAsync(SmsSettingKeys.CustomSendEnabled, false, cancellationToken),
                CustomTextEnabled = await GetBoolSettingAsync(SmsSettingKeys.CustomTextEnabled, false, cancellationToken),
                AllowImmediateSend = await GetBoolSettingAsync(SmsSettingKeys.AllowImmediateSend, false, cancellationToken),
                AllowRetryFailed = await GetBoolSettingAsync(SmsSettingKeys.AllowRetryFailed, true, cancellationToken),
                Message = account.IsSuccess ? "اتصال به SMS.ir برقرار است." : account.UserMessage ?? message
            };
        }

        public async Task<bool> CanViewFullMobileAsync(CancellationToken cancellationToken = default)
        {
            var maskByDefault = await GetBoolSettingAsync(
                SmsSettingKeys.MaskMobileInAdmin, true, cancellationToken);
            if (!maskByDefault)
                return true;

            return await GetBoolSettingAsync(
                SmsSettingKeys.AllowAdminViewFullMobile, false, cancellationToken);
        }

        public async Task<SmsActionResultDto> SendNotificationAsync(
            SendCustomNotificationRequestDto request,
            Guid adminUserId,
            CancellationToken cancellationToken = default)
        {
            await EnsureCustomAllowedAsync(adminUserId, textMode: false, cancellationToken);
            var mobile = await ResolveMobileAsync(request.Mobile, request.UserId, cancellationToken);
            var reference = request.OrderNumber?.Trim() ?? string.Empty;
            if (!SafeReference.IsMatch(reference))
                throw new BusinessException("کد پیگیری باید ۱ تا ۱۵۰ نویسه و فقط شامل حروف لاتین، عدد، خط تیره، نقطه یا زیرخط باشد.");

            var key = NormalizeIdempotency(request.IdempotencyKey, adminUserId, "notification", mobile, reference);
            var existing = await _db.SmsMessages.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
            if (existing is not null)
                return new SmsActionResultDto { SmsMessageId = existing.Id, Queued = existing.Status == 0, Success = existing.Status == 2, Message = "درخواست تکراری بود؛ رکورد موجود بازگردانده شد." };

            var parameters = SmsBusinessNotificationParameters.Create(reference);
            var immediate = request.SendImmediately && await GetBoolSettingAsync(SmsSettingKeys.AllowImmediateSend, false, cancellationToken);
            Guid historyId;
            if (immediate)
            {
                var result = await _sms.SendTemplateAsync(mobile, SmsTemplateKeys.UniversalNotification, parameters, cancellationToken);
                historyId = await _history.RecordDirectResultAsync(new SmsHistoryRecordRequest
                {
                    UserId = request.UserId,
                    Mobile = mobile,
                    Purpose = "AdminCustomNotification",
                    SendType = (byte)SmsSendType.NotificationTemplate,
                    TemplateKey = SmsTemplateKeys.UniversalNotification,
                    TemplateId = await _sms.GetTemplateIdAsync(SmsTemplateKeys.UniversalNotification, cancellationToken),
                    PublicReference = reference,
                    SafeMessagePreview = $"اعلان عمومی با کد پیگیری {reference}",
                    InternalNote = request.InternalNote?.Trim(),
                    CreatedByUserId = adminUserId,
                    RelatedEntityType = "AdminCustom",
                    RelatedEntityReference = reference,
                    IdempotencyKey = key
                }, result, cancellationToken);
                await AuditAsync(adminUserId, "SMS_CUSTOM_NOTIFICATION", result.IsSuccess, historyId, cancellationToken);
                return new SmsActionResultDto { SmsMessageId = historyId, Success = result.IsSuccess, Queued = false, Message = result.IsSuccess ? "پیامک ارسال شد." : result.UserMessage ?? "ارسال ناموفق بود." };
            }

            var aggregateId = Guid.NewGuid();
            await _outbox.EnqueueTemplateAsync(mobile, SmsTemplateKeys.UniversalNotification, parameters,
                "AdminCustomNotification", aggregateId, cancellationToken,
                request.UserId, adminUserId, "AdminCustom", reference, key, request.InternalNote);
            await _db.SaveChangesAsync(cancellationToken);
            historyId = await _db.SmsMessages.Where(x => x.IdempotencyKey == key).Select(x => x.Id).SingleAsync(cancellationToken);
            await AuditAsync(adminUserId, "SMS_CUSTOM_NOTIFICATION_QUEUED", true, historyId, cancellationToken);
            return new SmsActionResultDto { SmsMessageId = historyId, Success = true, Queued = true, Message = "پیامک در صف ارسال قرار گرفت." };
        }

        public async Task<SmsActionResultDto> SendTextAsync(
            SendCustomTextRequestDto request,
            Guid adminUserId,
            CancellationToken cancellationToken = default)
        {
            await EnsureCustomAllowedAsync(adminUserId, textMode: true, cancellationToken);
            var mobile = await ResolveMobileAsync(request.Mobile, request.UserId, cancellationToken);
            var text = request.Text?.Trim() ?? string.Empty;
            var maxLength = await GetIntSettingAsync(SmsSettingKeys.MaxCustomTextLength, 500, cancellationToken);
            if (text.Length == 0 || text.Length > maxLength)
                throw new BusinessException($"متن پیامک باید بین ۱ تا {maxLength} نویسه باشد.");
            if (Regex.IsMatch(text, "<[^>]+>") || text.Any(char.IsControl))
                throw new BusinessException("HTML، اسکریپت و نویسه‌های کنترلی در پیامک مجاز نیستند.");

            var key = NormalizeIdempotency(request.IdempotencyKey, adminUserId, "text", mobile, text);
            var existing = await _db.SmsMessages.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
            if (existing is not null)
                return new SmsActionResultDto { SmsMessageId = existing.Id, Queued = existing.Status == 0, Success = existing.Status == 2, Message = "درخواست تکراری بود؛ رکورد موجود بازگردانده شد." };

            var immediate = request.SendImmediately && await GetBoolSettingAsync(SmsSettingKeys.AllowImmediateSend, false, cancellationToken);
            Guid historyId;
            if (immediate)
            {
                var result = await _sms.SendTextAsync(mobile, text, cancellationToken);
                historyId = await _history.RecordDirectResultAsync(new SmsHistoryRecordRequest
                {
                    UserId = request.UserId,
                    Mobile = mobile,
                    Purpose = "AdminCustomText",
                    SendType = (byte)SmsSendType.CustomText,
                    SafeMessagePreview = text,
                    InternalNote = request.InternalNote?.Trim(),
                    CreatedByUserId = adminUserId,
                    RelatedEntityType = "AdminCustom",
                    IdempotencyKey = key
                }, result, cancellationToken);
                await AuditAsync(adminUserId, "SMS_CUSTOM_TEXT", result.IsSuccess, historyId, cancellationToken);
                return new SmsActionResultDto { SmsMessageId = historyId, Success = result.IsSuccess, Queued = false, Message = result.IsSuccess ? "پیامک متنی ارسال شد." : result.UserMessage ?? "ارسال ناموفق بود." };
            }

            var aggregateId = Guid.NewGuid();
            await _outbox.EnqueueTextAsync(mobile, text, "AdminCustomText", aggregateId, cancellationToken,
                request.UserId, adminUserId, "AdminCustom", null, key, request.InternalNote);
            await _db.SaveChangesAsync(cancellationToken);
            historyId = await _db.SmsMessages.Where(x => x.IdempotencyKey == key).Select(x => x.Id).SingleAsync(cancellationToken);
            await AuditAsync(adminUserId, "SMS_CUSTOM_TEXT_QUEUED", true, historyId, cancellationToken);
            return new SmsActionResultDto { SmsMessageId = historyId, Success = true, Queued = true, Message = "پیامک متنی در صف قرار گرفت." };
        }

        public async Task RetryAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default)
        {
            if (!await GetBoolSettingAsync(SmsSettingKeys.AllowRetryFailed, true, cancellationToken))
                throw new BusinessException("بازتلاش پیامک در تنظیمات غیرفعال است.");

            var sms = await _db.SmsMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("رکورد پیامک یافت نشد.");
            if (sms.Status == (byte)SmsMessageStatus.Sent)
                throw new BusinessException("پیامک ارسال‌شده قابل بازتلاش نیست.");
            if (sms.SendType == (byte)SmsSendType.OtpTemplate)
                throw new BusinessException("کد OTP برای امنیت ذخیره نمی‌شود و قابل بازتلاش دستی نیست.");
            if (!Enum.TryParse<SmsFailureReason>(sms.ProviderErrorCode, out var reason) || !SmsRetryPolicy.IsRetryable(reason))
                throw new BusinessException("این خطا قابل بازتلاش نیست.");
            if (!sms.OutboxMessageId.HasValue)
                throw new BusinessException("این پیامک صف ذخیره‌شده برای بازتلاش ندارد.");

            var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == sms.OutboxMessageId, cancellationToken)
                ?? throw new NotFoundException("پیام صف مرتبط یافت نشد.");
            outbox.Status = (byte)OutboxMessageStatus.Pending;
            outbox.ErrorMessage = null;
            outbox.ProcessedAt = null;
            sms.Status = (byte)SmsMessageStatus.Retrying;
            sms.NextRetryAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(adminUserId, "SMS_RETRY", true, id, cancellationToken);
        }

        public async Task CancelAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default)
        {
            var sms = await _db.SmsMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("رکورد پیامک یافت نشد.");
            if (sms.Status is not ((byte)SmsMessageStatus.Pending) and not ((byte)SmsMessageStatus.Retrying))
                throw new BusinessException("فقط پیامک در انتظار یا در حال بازتلاش قابل لغو است.");
            if (sms.OutboxMessageId.HasValue)
            {
                var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == sms.OutboxMessageId, cancellationToken);
                if (outbox is not null) outbox.Status = (byte)OutboxMessageStatus.Failed;
            }
            sms.Status = (byte)SmsMessageStatus.Cancelled;
            sms.FailedAt = DateTime.UtcNow;
            sms.ProviderErrorCode = "CancelledByAdmin";
            sms.ProviderErrorMessage = "ارسال توسط مدیر لغو شد.";
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(adminUserId, "SMS_CANCEL", true, id, cancellationToken);
        }

        public async Task<string> ExportCsvAsync(SmsHistoryFilterDto filter, CancellationToken cancellationToken = default)
        {
            var rows = await ApplyFilter(_db.SmsMessages.AsNoTracking(), filter)
                .OrderByDescending(x => x.CreatedAt)
                .Take(10_000)
                .Select(x => new
                {
                    x.Status, x.MaskedMobile, x.SendType, x.Purpose, x.PublicReference,
                    x.CreatedAt, x.SentAt, x.RetryCount, x.ProviderMessageId
                })
                .ToListAsync(cancellationToken);
            var sb = new StringBuilder("Status,Mobile,Type,Purpose,Reference,CreatedAt,SentAt,RetryCount,ProviderMessageId\r\n");
            foreach (var x in rows)
                sb.AppendLine(string.Join(',', Csv(StatusName(x.Status)), Csv(x.MaskedMobile), Csv(SendTypeName(x.SendType)), Csv(x.Purpose), Csv(x.PublicReference), Csv(x.CreatedAt.ToString("O")), Csv(x.SentAt?.ToString("O")), x.RetryCount, Csv(x.ProviderMessageId)));
            return sb.ToString();
        }

        private IQueryable<SmsMessage> ApplyFilter(IQueryable<SmsMessage> query, SmsHistoryFilterDto f)
        {
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(x => x.Mobile.Contains(term) ||
                                         (x.PublicReference != null && x.PublicReference.Contains(term)) ||
                                         x.Purpose.Contains(term) ||
                                         (x.ProviderMessageId != null && x.ProviderMessageId.Contains(term)));
            }
            if (f.Status.HasValue) query = query.Where(x => x.Status == f.Status.Value);
            if (f.SendType.HasValue) query = query.Where(x => x.SendType == f.SendType.Value);
            if (!string.IsNullOrWhiteSpace(f.Purpose)) query = query.Where(x => x.Purpose == f.Purpose);
            if (!string.IsNullOrWhiteSpace(f.Provider)) query = query.Where(x => x.Provider == f.Provider);
            if (!string.IsNullOrWhiteSpace(f.TemplateKey)) query = query.Where(x => x.TemplateKey == f.TemplateKey);
            if (f.CreatedByUserId.HasValue) query = query.Where(x => x.CreatedByUserId == f.CreatedByUserId);
            if (f.DateFrom.HasValue) query = query.Where(x => x.CreatedAt >= f.DateFrom.Value);
            if (f.DateTo.HasValue) query = query.Where(x => x.CreatedAt < f.DateTo.Value.AddDays(1));
            if (f.OnlyFailed) query = query.Where(x => x.Status == (byte)SmsMessageStatus.Failed || x.Status == (byte)SmsMessageStatus.DeadLetter);
            if (f.OnlyPending) query = query.Where(x => x.Status == (byte)SmsMessageStatus.Pending || x.Status == (byte)SmsMessageStatus.Retrying || x.Status == (byte)SmsMessageStatus.Processing);
            if (f.OnlyCustom) query = query.Where(x => x.SendType == (byte)SmsSendType.CustomText);
            return query;
        }

        private async Task EnsureCustomAllowedAsync(Guid adminUserId, bool textMode, CancellationToken ct)
        {
            if (adminUserId == Guid.Empty) throw new UnauthorizedException("مدیر احراز هویت نشده است.");
            if (!await GetBoolSettingAsync(SmsSettingKeys.CustomSendEnabled, false, ct))
                throw new BusinessException("ارسال سفارشی پیامک غیرفعال است.");
            if (textMode && !await GetBoolSettingAsync(SmsSettingKeys.CustomTextEnabled, false, ct))
                throw new BusinessException("ارسال پیامک متنی غیرفعال است.");
            var since = DateTime.UtcNow.AddMinutes(-1);
            if (await _db.SmsMessages.CountAsync(x => x.CreatedByUserId == adminUserId && x.CreatedAt >= since, ct) >= 10)
                throw new BusinessException("تعداد ارسال‌های سفارشی بیش از حد مجاز است. یک دقیقه بعد تلاش کنید.");
        }

        private async Task<string> ResolveMobileAsync(string input, Guid? userId, CancellationToken ct)
        {
            var mobile = input;
            if (userId.HasValue)
                mobile = await _db.Users.Where(x => x.Id == userId && !x.IsDeleted).Select(x => x.Mobile).FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("کاربر انتخاب‌شده یافت نشد.");
            if (!IranMobile.TryNormalize(mobile, out var normalized))
                throw new BusinessException("شماره موبایل معتبر نیست.");
            return normalized;
        }

        private async Task<bool> GetBoolSettingAsync(string key, bool fallback, CancellationToken ct)
        {
            var value = await _db.Settings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
            return bool.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
        {
            var value = await _db.Settings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private async Task AuditAsync(Guid adminId, string action, bool success, Guid messageId, CancellationToken ct) =>
            await _securityLog.LogAsync(adminId, action, success, $"SmsMessage={messageId:N}");

        private static string NormalizeIdempotency(string? supplied, Guid adminId, string mode, string mobile, string content) =>
            string.IsNullOrWhiteSpace(supplied)
                ? $"admin:{adminId:N}:{mode}:{mobile}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).Substring(0, 16)}:{DateTime.UtcNow:yyyyMMddHHmm}"
                : $"admin:{adminId:N}:{supplied.Trim()}";

        private static SmsHistoryItemDto Map(SmsMessage x, bool full) => new()
        {
            Id = x.Id, Mobile = full ? x.Mobile : x.MaskedMobile, MaskedMobile = x.MaskedMobile,
            Purpose = x.Purpose, SendType = x.SendType, SendTypeName = SendTypeName(x.SendType),
            TemplateKey = x.TemplateKey, TemplateId = x.TemplateId, PublicReference = x.PublicReference,
            Provider = x.Provider, Status = x.Status, StatusName = StatusName(x.Status),
            ProviderMessageId = x.ProviderMessageId, ProviderErrorCode = x.ProviderErrorCode,
            ProviderErrorMessage = x.ProviderErrorMessage, DeliveryCost = x.DeliveryCost,
            RetryCount = x.RetryCount, MaxRetryCount = x.MaxRetryCount, CreatedAt = x.CreatedAt,
            LastAttemptAt = x.LastAttemptAt, SentAt = x.SentAt, FailedAt = x.FailedAt,
            NextRetryAt = x.NextRetryAt, CreatedByUserId = x.CreatedByUserId,
            CreatedByName = x.CreatedByUser?.FullName, RelatedEntityType = x.RelatedEntityType,
            RelatedEntityId = x.RelatedEntityId, RelatedEntityReference = x.RelatedEntityReference,
            SafeMessagePreview = x.SafeMessagePreview, InternalNote = x.InternalNote,
            CorrelationId = x.CorrelationId
        };

        public static string StatusName(byte status) => (SmsMessageStatus)status switch
        {
            SmsMessageStatus.Pending => "در انتظار", SmsMessageStatus.Processing => "در حال ارسال",
            SmsMessageStatus.Sent => "ارسال‌شده", SmsMessageStatus.Failed => "ناموفق",
            SmsMessageStatus.Retrying => "بازتلاش", SmsMessageStatus.DeadLetter => "متوقف‌شده",
            SmsMessageStatus.Disabled => "غیرفعال", SmsMessageStatus.Cancelled => "لغوشده", _ => "نامشخص"
        };

        public static string SendTypeName(byte type) => (SmsSendType)type switch
        {
            SmsSendType.OtpTemplate => "قالب OTP", SmsSendType.NotificationTemplate => "قالب اطلاع‌رسانی",
            SmsSendType.CustomText => "متن سفارشی", _ => "نامشخص"
        };

        private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}
