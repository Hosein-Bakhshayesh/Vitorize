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

            // کاربران پیش‌فرض پس از ذخیره‌ی نقش‌ها ساخته می‌شوند تا نقش‌ها قابل واکشی باشند.
            await SeedDefaultUsersAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task SeedDefaultUsersAsync(CancellationToken cancellationToken)
        {
            await SeedUserAsync(
                mobile: "09123456789",
                fullName: "مدیر سیستم",
                password: "12345678",
                roleNames: new[] { "SuperAdmin", "Admin" },
                cancellationToken);

            await SeedUserAsync(
                mobile: "09378149896",
                fullName: "مشتری نمونه",
                password: "123456",
                roleNames: new[] { "Customer" },
                cancellationToken);
        }

        private async Task SeedUserAsync(
            string mobile,
            string fullName,
            string password,
            string[] roleNames,
            CancellationToken cancellationToken)
        {
            // اگر کاربر از قبل وجود دارد هیچ‌چیزی بازنویسی نمی‌شود (ایمن برای Production).
            var exists = await _dbContext.Users
                .AnyAsync(x => x.Mobile == mobile, cancellationToken);

            if (exists)
                return;

            var roles = await _dbContext.Roles
                .Where(x => roleNames.Contains(x.Name))
                .ToListAsync(cancellationToken);

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Mobile = mobile,
                PasswordHash = PasswordHasher.Hash(password),
                Status = (byte)Vitorize.Shared.Enums.UserStatus.Active,
                VerificationStatus = (byte)Vitorize.Shared.Enums.VerificationStatus.Pending,
                IsMobileConfirmed = true,
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            foreach (var role in roles)
                user.Roles.Add(role);

            await _dbContext.Users.AddAsync(user, cancellationToken);
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
                S("SiteName", "ویتورایز", "General", "string", "نام فروشگاه"),
                S("SiteDescription", "فروشگاه گیفت کارت و سرویس‌های دیجیتال", "General", "string", "توضیح کوتاه فروشگاه"),
                S("SiteTagline", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین", "Branding", "string", "شعار سایت (کنار لوگو و عنوان صفحات)"),
                S("SiteLogoPath", "", "Branding", "string", "مسیر لوگوی سایت (خالی = لوگوی پیش‌فرض)"),
                S("HeroKicker", "ویتورایز · بازارگاه دیجیتال", "Branding", "string", "متن کوچک بالای عنوان Hero"),
                S("HeroTitle", "دنیای بازی و دیجیتال در دستان تو", "Branding", "string", "عنوان اصلی Hero صفحه اول"),
                S("HeroSubtitle", "خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.", "Branding", "string", "زیرعنوان Hero صفحه اول"),
                S("HeroCtaText", "ورود به فروشگاه", "Branding", "string", "متن دکمه Hero"),
                S("FooterDescription", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین؛ خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی.", "Branding", "string", "توضیح فوتر"),
                S("CopyrightText", "تمامی حقوق برای ویتورایز محفوظ است.", "Branding", "string", "متن کپی‌رایت فوتر"),
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
