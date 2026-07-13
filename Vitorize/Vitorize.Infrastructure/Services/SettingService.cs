using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class SettingService : ISettingService
    {
        // مقدار نمایشی برای کلیدهای محرمانه؛ هرگز مقدار واقعی به کلاینت/ادمین برنمی‌گردد.
        private const string SecretMask = "********";

        private readonly VitorizeDbContext _dbContext;
        private readonly ISmsSettingsProvider _smsSettingsProvider;

        public SettingService(
            VitorizeDbContext dbContext,
            ISmsSettingsProvider smsSettingsProvider)
        {
            _dbContext = dbContext;
            _smsSettingsProvider = smsSettingsProvider;
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
            "Features", "Scripts"
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

            var setting = await _dbContext.Settings
                .FirstOrDefaultAsync(x => x.Key == key);

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

            // باطل‌کردن کش تنظیمات پیامک تا تغییرات بلافاصله اعمال شود.
            if (key.StartsWith("Sms.", StringComparison.OrdinalIgnoreCase))
                _smsSettingsProvider.Invalidate();

            return Map(setting);
        }

        public async Task DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new BusinessException("کلید تنظیمات معتبر نیست.");

            var setting = await _dbContext.Settings
                .FirstOrDefaultAsync(x => x.Key == key.Trim());

            if (setting == null)
                throw new NotFoundException("تنظیمات یافت نشد.");

            _dbContext.Settings.Remove(setting);

            await _dbContext.SaveChangesAsync();

            if (setting.Key.StartsWith("Sms.", StringComparison.OrdinalIgnoreCase))
                _smsSettingsProvider.Invalidate();
        }

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