using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class VitorizeSeedService : IVitorizeSeedService
    {
        private readonly VitorizeDbContext _dbContext;
        public VitorizeSeedService(VitorizeDbContext dbContext) => _dbContext = dbContext;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await SeedRolesAsync(cancellationToken);
            await SeedSettingsAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task SeedRolesAsync(CancellationToken cancellationToken)
        {
            var roles = new[]
            {
                ("SuperAdmin", "مدیر کل"),
                ("Admin", "مدیر فروشگاه"),
                ("Support", "پشتیبان"),
                ("Customer", "مشتری")
            };

            foreach (var role in roles)
            {
                var exists = await _dbContext.Roles.AnyAsync(x => x.Name == role.Item1, cancellationToken);
                if (!exists)
                {
                    await _dbContext.Roles.AddAsync(new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = role.Item1,
                        DisplayName = role.Item2,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }
        }

        private async Task SeedSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = new[]
            {
                S("SiteName", "Vitorize", "General", "string", "نام فروشگاه"),
                S("SiteDescription", "فروشگاه گیفت کارت و سرویس‌های دیجیتال", "General", "string", "توضیح کوتاه فروشگاه"),
                S("SupportEmail", "support@vitorize.com", "Support", "string", "ایمیل پشتیبانی"),
                S("SupportPhone", "02100000000", "Support", "string", "شماره پشتیبانی"),
                S("InstagramUrl", "https://instagram.com/vitorize", "Social", "string", "صفحه اینستاگرام"),
                S("TelegramUrl", "https://t.me/vitorize", "Social", "string", "کانال تلگرام"),
                S("EnableRegistration", "true", "Features", "bool", "ثبت‌نام کاربران"),
                S("EnableWallet", "true", "Features", "bool", "کیف پول کاربران"),
                S("SmsEnabled", "false", "SMS", "bool", "ارسال پیامک"),
                S("SmsProvider", "Mock", "SMS", "string", "ارائه‌دهنده پیامک"),
                S("WalletMinCharge", "100000", "Wallet", "decimal", "حداقل شارژ کیف پول"),
                S("WalletMaxCharge", "100000000", "Wallet", "decimal", "حداکثر شارژ کیف پول"),
                S("ZarinpalMerchantId", "", "Payment", "string", "شناسه پذیرنده زرین‌پال"),
                S("ZarinpalSandbox", "true", "Payment", "bool", "حالت آزمایشی زرین‌پال"),
                S("ZarinpalStartPayUrl", "https://sandbox.zarinpal.com/pg/StartPay", "Payment", "string", "آدرس شروع پرداخت زرین‌پال"),
                S("ZarinpalBaseUrl", "https://sandbox.zarinpal.com/pg/v4/payment", "Payment", "string", "آدرس اصلی زرین‌پال"),
                S("ZarinpalCallbackUrl", "https://localhost:7221/api/payments/zarinpal/callback", "Payment", "string", "آدرس بازگشت پرداخت زرین‌پال")
            };

            foreach (var item in settings)
            {
                var current = await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == item.Key, cancellationToken);
                if (current == null)
                {
                    await _dbContext.Settings.AddAsync(new Setting
                    {
                        Id = Guid.NewGuid(),
                        Key = item.Key,
                        Value = item.Value,
                        GroupName = item.GroupName,
                        ValueType = item.ValueType,
                        Description = item.Description,
                        UpdatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
                else
                {
                    current.GroupName = string.IsNullOrWhiteSpace(current.GroupName) ? item.GroupName : current.GroupName;
                    current.ValueType = string.IsNullOrWhiteSpace(current.ValueType) ? item.ValueType : current.ValueType;
                    current.Description = string.IsNullOrWhiteSpace(current.Description) ? item.Description : current.Description;
                }
            }
        }

        private static SeedSetting S(string key, string value, string groupName, string valueType, string description) =>
            new(key, value, groupName, valueType, description);

        private sealed record SeedSetting(string Key, string Value, string GroupName, string ValueType, string Description);
    }
}
