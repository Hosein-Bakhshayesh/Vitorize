using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// تأمین‌کننده امن و کش‌شده تنظیمات پیامک از جدول Settings.
    /// TTL کوتاه دارد و هنگام تغییر تنظیمات توسط ادمین باطل می‌شود.
    /// </summary>
    public interface ISmsSettingsProvider
    {
        Task<SmsOptions> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>باطل‌کردن کش پس از به‌روزرسانی تنظیمات پیامک.</summary>
        void Invalidate();
    }
}
