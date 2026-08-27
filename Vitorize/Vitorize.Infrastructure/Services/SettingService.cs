using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Storefront;

namespace Vitorize.Infrastructure.Services
{
    public class SettingService : ISettingService
    {
        // مقدار نمایشی برای کلیدهای محرمانه؛ هرگز مقدار واقعی به کلاینت/ادمین برنمی‌گردد.
        private const string SecretMask = "********";

        private readonly VitorizeDbContext _dbContext;
        private readonly ISmsSettingsProvider _smsSettingsProvider;
        private readonly IMaintenanceStateProvider _maintenanceState;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUser;

        public SettingService(
            VitorizeDbContext dbContext,
            ISmsSettingsProvider smsSettingsProvider,
            IMaintenanceStateProvider maintenanceState,
            IAuditService auditService,
            ICurrentUserService currentUser)
        {
            _dbContext = dbContext;
            _smsSettingsProvider = smsSettingsProvider;
            _maintenanceState = maintenanceState;
            _auditService = auditService;
            _currentUser = currentUser;
        }

        private static bool IsSecret(string key) =>
            SmsSettingKeys.SecretKeys.Contains(key);

        public async Task<List<SettingGroupDto>> GetAllGroupedAsync()
        {
            var settings = await _dbContext.Settings
                .AsNoTracking()
                .OrderBy(x => x.GroupName)
                .ThenBy(x => x.Key)
                .Select(x => Map(x))
                .ToListAsync();

            return settings
                .Where(x => !SmsSettingKeys.DeprecatedKeys.Contains(x.Key))
                .GroupBy(x => string.IsNullOrWhiteSpace(x.GroupName) ? "General" : x.GroupName)
                .Select(x => new SettingGroupDto
                {
                    GroupName = x.Key,
                    Settings = x.ToList()
                })
                .ToList();
        }

        // گروه‌هایی که برای مشتری/فروشگاه قابل نمایش‌اند. هر تنظیم داخل این گروه‌ها از
        // طریق «settings/public» بدون احراز هویت در دسترس است؛ بنابراین هرگز نباید مقادیر
        // محرمانه (پرداخت، پیامک، ایمیل، امنیت، آپلود، کیف‌پول) در این گروه‌ها قرار گیرند.
        private static readonly HashSet<string> PublicGroups = new(StringComparer.OrdinalIgnoreCase)
        {
            "General", "Branding", "Logos", "SEO", "Homepage", "About", "Trust",
            "Footer", "Social", "Contact", "Support", "Empty", "Errors",
            "Features", "Typography", "TrustSeals", "Scripts"
        };

        public async Task<List<SettingDto>> GetPublicSettingsAsync()
        {
            var all = await _dbContext.Settings
                .AsNoTracking()
                .OrderBy(x => x.GroupName)
                .ThenBy(x => x.Key)
                .Select(x => Map(x))
                .ToListAsync();

            return all
                .Where(x => x.GroupName != null && PublicGroups.Contains(x.GroupName))
                .Where(x => !IsSecret(x.Key)) // دفاع در عمق: هرگز کلید محرمانه در پاسخ عمومی نباشد
                .ToList();
        }

        public async Task<SettingDto?> GetByKeyAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new BusinessException("کلید تنظیمات معتبر نیست.");

            if (SmsSettingKeys.DeprecatedKeys.Contains(key.Trim()))
                return null;

            var setting = await _dbContext.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == key.Trim());

            return setting == null ? null : Map(setting);
        }

        public async Task<SettingDto> UpsertAsync(UpdateSettingDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
                throw new BusinessException("کلید تنظیمات الزامی است.");

            var key = request.Key.Trim();
            if (SmsSettingKeys.DeprecatedKeys.Contains(key))
                throw new BusinessException("این تنظیم دیگر استفاده نمی‌شود؛ ارسال پیامک سفارشی همیشه فعال است.");
            TrustedSiteMarkupRules.ValidateSetting(key, request.Value);
            VatSettings.ValidateSetting(key, request.Value);
            try
            {
                OrderKycSettings.ValidateSetting(key, request.Value);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessException(ex.Message);
            }

            // Reject an unsupported ordering rather than quietly coercing it. The query layer already
            // falls back to the default, which is precisely why nobody noticed the admin control was a
            // free-text box: whatever was typed simply had no effect. Saying so is more useful.
            if (string.Equals(key, StorefrontProductSortModes.SettingKey, StringComparison.OrdinalIgnoreCase) &&
                !StorefrontProductSortModes.IsSupported(request.Value))
            {
                throw new BusinessException(
                    "ترتیب پیش‌فرض انتخاب‌شده معتبر نیست. یکی از گزینه‌های موجود را انتخاب کنید.");
            }

            // Store the canonical spelling. Codes are matched case-insensitively, so "newest" is
            // accepted - but the admin select compares against the canonical "Newest", and a
            // differently-cased row would leave the control showing nothing selected.
            if (string.Equals(key, StorefrontProductSortModes.SettingKey, StringComparison.OrdinalIgnoreCase))
                request.Value = StorefrontProductSortModes.Normalize(request.Value);
            if (key is "TrustBadgesJson" or "HomeFeaturesJson")
                request.Value = LucideIconRules.NormalizeConfigurableBlocksJson(request.Value);

            if (SmsSettingKeys.TryGetTemplateIdGroup(key, out var templateGroup))
                return await UpsertTemplateIdGroupAsync(key, request, templateGroup);

            var setting = await _dbContext.Settings
                .FirstOrDefaultAsync(x => x.Key == key);
            var previousValue = setting?.Value;

            if (string.Equals(request.ValueType, "icon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting?.ValueType, "icon", StringComparison.OrdinalIgnoreCase))
                request.Value = LucideIconRules.NormalizeOptional(request.Value);

            if (setting == null)
            {
                setting = new Setting
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    UpdatedAt = DateTime.UtcNow
                };

                await _dbContext.Settings.AddAsync(setting);
            }

            // برای کلید محرمانه: اگر مقدار ارسالی همان ماسک باشد یعنی «بدون تغییر»؛ مقدار فعلی حفظ می‌شود
            // تا سهواً کلید واقعی با ماسک بازنویسی نشود.
            if (IsSecret(key) && request.Value == SecretMask)
            {
                // مقدار را دست‌نخورده نگه می‌داریم.
            }
            else
            {
                setting.Value = request.Value;
            }

            setting.GroupName = request.GroupName?.Trim();
            setting.ValueType = request.ValueType?.Trim();
            setting.Description = request.Description?.Trim();
            setting.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // VAT settings drive money calculations, so a change is recorded through the existing
            // audit service. Scoped deliberately to VAT keys only; no broader settings-audit refactor.
            if (VatSettings.IsVatKey(key))
                await _auditService.LogAsync(
                    _currentUser.UserId,
                    "SettingUpdated",
                    nameof(Setting),
                    key,
                    $"old={previousValue ?? string.Empty}; new={setting.Value ?? string.Empty}",
                    _currentUser.IpAddress,
                    _currentUser.UserAgent);

            if (string.Equals(setting.GroupName, "Logos", StringComparison.OrdinalIgnoreCase))
                await BumpBrandAssetVersionAsync();

            // باطل‌کردن کش تنظیمات پیامک تا تغییرات بلافاصله اعمال شود.
            if (key.StartsWith("Sms.", StringComparison.OrdinalIgnoreCase))
                _smsSettingsProvider.Invalidate();

            // Maintenance mode is enforced per request from a cached read, so the switch has to take
            // effect now rather than when the cache happens to expire.
            if (string.Equals(key, "MaintenanceMode", StringComparison.OrdinalIgnoreCase))
                _maintenanceState.Invalidate();

            return Map(setting);
        }

        public async Task DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new BusinessException("کلید تنظیمات معتبر نیست.");

            key = key.Trim();

            if (SmsSettingKeys.TryGetTemplateIdGroup(key, out var templateGroup))
            {
                var synchronizedSettings = await _dbContext.Settings
                    .Where(x => templateGroup.Contains(x.Key))
                    .ToListAsync();

                if (synchronizedSettings.Count == 0)
                    throw new NotFoundException("تنظیمات یافت نشد.");

                foreach (var item in synchronizedSettings)
                {
                    item.Value = string.Empty;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                _smsSettingsProvider.Invalidate();
                return;
            }

            var setting = await _dbContext.Settings
                .FirstOrDefaultAsync(x => x.Key == key);

            if (setting == null)
                throw new NotFoundException("تنظیمات یافت نشد.");

            _dbContext.Settings.Remove(setting);

            await _dbContext.SaveChangesAsync();

            if (setting.Key.StartsWith("Sms.", StringComparison.OrdinalIgnoreCase))
                _smsSettingsProvider.Invalidate();
        }

        private async Task<SettingDto> UpsertTemplateIdGroupAsync(
            string requestedKey,
            UpdateSettingDto request,
            IReadOnlyList<string> templateGroup)
        {
            var value = request.Value?.Trim() ?? string.Empty;
            if (value.Length > 0 && (!int.TryParse(value, out var templateId) || templateId <= 0))
                throw new BusinessException("شناسه قالب پیامک باید یک عدد صحیح مثبت باشد.");

            var existing = await _dbContext.Settings
                .Where(x => templateGroup.Contains(x.Key))
                .ToListAsync();
            var byKey = existing.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var groupKey in templateGroup)
            {
                if (!byKey.TryGetValue(groupKey, out var item))
                {
                    item = new Setting
                    {
                        Id = Guid.NewGuid(),
                        Key = groupKey,
                        GroupName = SmsSettingKeys.Group,
                        ValueType = "int",
                        Description = TemplateDescription(groupKey)
                    };
                    await _dbContext.Settings.AddAsync(item);
                    byKey[groupKey] = item;
                }

                item.Value = value;
                item.UpdatedAt = now;
            }

            var requested = byKey[requestedKey];
            requested.GroupName = request.GroupName?.Trim() ?? requested.GroupName ?? SmsSettingKeys.Group;
            requested.ValueType = request.ValueType?.Trim() ?? requested.ValueType ?? "int";
            requested.Description = request.Description?.Trim() ?? requested.Description ?? TemplateDescription(requestedKey);

            await _dbContext.SaveChangesAsync();

            _smsSettingsProvider.Invalidate();
            return Map(requested);
        }

        private async Task BumpBrandAssetVersionAsync()
        {
            var version = await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == "Branding.AssetVersion");
            if (version is null)
            {
                version = new Setting { Id = Guid.NewGuid(), Key = "Branding.AssetVersion", GroupName = "Branding", ValueType = "string" };
                await _dbContext.Settings.AddAsync(version);
            }
            version.Value = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            version.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        private static string TemplateDescription(string key) => key switch
        {
            SmsSettingKeys.OtpTemplateId => "شناسه قالب کد یکبار مصرف",
            SmsSettingKeys.NotificationTemplateId => "شناسه قالب اطلاع‌رسانی عمومی",
            _ when SmsSettingKeys.OtpTemplateIdKeys.Contains(key, StringComparer.OrdinalIgnoreCase) =>
                "کلید سازگاری قالب OTP؛ با Sms.OtpTemplateId همگام می‌شود (CODE، EXPIRE)",
            _ => "کلید سازگاری قالب اطلاع رسانی؛ با Sms.NotificationTemplateId همگام می‌شود (ORDER_NUMBER)"
        };

        private static SettingDto Map(Setting setting)
        {
            // کلیدهای محرمانه به‌صورت ماسک‌شده برگردانده می‌شوند (اگر مقدار داشته باشند).
            var value = setting.Value;
            if (IsSecret(setting.Key) && !string.IsNullOrEmpty(value))
                value = SecretMask;

            return new SettingDto
            {
                Id = setting.Id,
                Key = setting.Key,
                Value = value,
                GroupName = setting.GroupName,
                ValueType = setting.ValueType,
                Description = setting.Description,
                UpdatedAt = setting.UpdatedAt
            };
        }

        public async Task<string?> GetValueAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new BusinessException("کلید تنظیمات معتبر نیست.");

            return await _dbContext.Settings
                .AsNoTracking()
                .Where(x => x.Key == key.Trim())
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
        }

        public async Task<T?> GetValueAsync<T>(string key)
        {
            var value = await GetValueAsync(key);

            if (string.IsNullOrWhiteSpace(value))
                return default;

            try
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                if (targetType == typeof(string))
                    return (T)(object)value;

                if (targetType == typeof(bool))
                    return (T)(object)bool.Parse(value);

                if (targetType == typeof(int))
                    return (T)(object)int.Parse(value);

                if (targetType == typeof(decimal))
                    return (T)(object)decimal.Parse(value);

                if (targetType == typeof(Guid))
                    return (T)(object)Guid.Parse(value);

                return (T?)Convert.ChangeType(value, targetType);
            }
            catch
            {
                throw new BusinessException($"مقدار تنظیمات برای کلید {key} معتبر نیست.");
            }
        }
    }
}
